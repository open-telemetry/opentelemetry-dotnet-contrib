// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class GenevaTraceExporterTests : IDisposable
{
    private readonly Dictionary<string, Action<Dictionary<object, object>>> expectedMappingChecks = [];

    public GenevaTraceExporterTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    }

    [Fact]
    public void GenevaTraceExporter_constructor_Invalid_Input()
    {
        // no connection string
        Assert.Throws<ArgumentException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions());
        });

        // null connection string
        Assert.Throws<ArgumentException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = null,
            });
        });

        // null value in the PrepopulatedFields
        Assert.Throws<ArgumentNullException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = null,
                },
            });
        });

        // unsupported types(char) for PrepopulatedFields
        Assert.Throws<ArgumentException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = (char)106,
                },
            });
        });

        // Supported types for PrepopulatedFields should not throw an exception
        var exception = Record.Exception(() =>
        {
            _ = new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["bool"] = true,
                    ["byte"] = byte.MaxValue,
                    ["sbyte"] = sbyte.MaxValue,
                    ["short"] = short.MaxValue,
                    ["ushort"] = ushort.MaxValue,
                    ["int"] = int.MaxValue,
                    ["uint"] = uint.MaxValue,
                    ["long"] = long.MaxValue,
                    ["ulong"] = ulong.MaxValue,
                    ["float"] = float.MaxValue,
                    ["double"] = double.MaxValue,
                    ["string"] = string.Empty,
                },
            };
        });

        Assert.Null(exception);
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Windows)]
    public void GenevaTraceExporter_constructor_Invalid_Input_Windows()
    {
        // no ETW session name
        Assert.Throws<NotSupportedException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "key=value",
            });
        });

        // empty ETW session name
        Assert.Throws<ArgumentException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=",
            });
        });
    }

    [Fact]
    public void GenevaTraceExporter_TableNameMappings_SpecialCharacters()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            using var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                TableNameMappings = new Dictionary<string, string> { ["Span"] = "\u0418" },
            });
        });
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Windows)]
    public void GenevaTraceExporter_Success_Windows()
    {
        // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
        // the ActivitySource used by other unit tests.
        var sourceName = GetTestMethodName();

        // TODO: Setup a mock or spy for eventLogger to assert that eventLogger.LogInformationalEvent is actually called.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(sourceName)
            .AddGenevaTraceExporter(options =>
            {
                options.ConnectionString = "EtwSession=OpenTelemetry";
                options.PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = "9.0.15289.2",
                };
            })
            .Build();

        var source = new ActivitySource(sourceName);
        using (var parent = source.StartActivity("HttpIn", ActivityKind.Server))
        {
            parent.SetTag("http.method", "GET");
            parent.SetTag("http.url", "https://localhost/wiki/Rabbit");
            using (var child = source.StartActivity("HttpOut", ActivityKind.Client))
            {
                child.SetTag("http.method", "GET");
                child.SetTag("http.url", "https://www.wikipedia.org/wiki/Rabbit");
                child.SetTag("http.status_code", 404);
            }

            parent?.SetTag("http.status_code", 200);
        }

        var link = new ActivityLink(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        using (var activity = source.StartActivity("Foo", ActivityKind.Internal, null, null, [link]))
        {
        }

        using (var activity = source.StartActivity("Bar"))
        {
            activity.SetStatus(ActivityStatusCode.Error);
        }

        using (var activity = source.StartActivity("Baz"))
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void GenevaTraceExporter_Serialization_Success(bool hasTableNameMapping, bool hasCustomFields, bool includeTraceState)
    {
        var path = string.Empty;
        Socket server = null;
        try
        {
            var invocationCount = 0;
            var exporterOptions = new GenevaExporterOptions
            {
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
            }
            else
            {
                path = GetRandomFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path;
                var endpoint = new UnixDomainSocketEndPoint(path);
                server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                server.Bind(endpoint);
                server.Listen(1);
            }

            if (hasTableNameMapping)
            {
                exporterOptions.TableNameMappings = new Dictionary<string, string> { { "Span", "CustomActivity" } };
            }

            if (hasCustomFields)
            {
                // The tag "clientRequestId" should be present in the mapping as a separate key. Other tags which are not present
                // in the m_dedicatedFields should be added in the mapping under "env_properties"
                exporterOptions.CustomFields = ["clientRequestId"];
            }

            if (includeTraceState)
            {
                exporterOptions.IncludeTraceStateForSpan = true;
            }

            using var exporter = new MsgPackTraceExporter(exporterOptions);
            var m_buffer = exporter.Buffer;

            // Add an ActivityListener to serialize the activity and assert that it was valid on ActivityStopped event

            // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
            // the ActivitySource used by other unit tests.
            var sourceName = GetTestMethodName();

            Dictionary<string, object> resourceAttributes = new Dictionary<string, object>
            {
                { "resourceAttribute", "resourceValue" },
                { "activityTag", "causes conflict" },
                { "service.name", "BusyWorker" },
                { "service.instanceId", "CY1SCH030021417" },
            };
            var resource = new Resource(resourceAttributes);

            using var listener = new ActivityListener();
            listener.ShouldListenTo = (activitySource) => activitySource.Name == sourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = (activity) =>
            {
                _ = exporter.SerializeActivity(activity, resource);
                var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Options);
                this.CheckSpanForActivity(exporterOptions, fluentdData, activity, exporter.DedicatedFields, resourceAttributes);

                Console.WriteLine(activity.OperationName);
                Console.WriteLine(invocationCount + 1);
                invocationCount++;
            };
            ActivitySource.AddActivityListener(listener);

            using var source = new ActivitySource(sourceName);
            using (var parentActivity = source.StartActivity("ParentActivity"))
            {
                var linkedtraceId1 = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686a1".AsSpan());
                var linkedSpanId1 = ActivitySpanId.CreateFromString("888915b6286b9c01".AsSpan());

                var linkedtraceId2 = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686a2".AsSpan());
                var linkedSpanId2 = ActivitySpanId.CreateFromString("888915b6286b9c02".AsSpan());

                if (includeTraceState)
                {
                    parentActivity.TraceStateString = "some=state";
                }

                this.ExpectSpanFromActivity(parentActivity, (mapping) => { });

                var links = new[]
                {
                    new ActivityLink(new ActivityContext(
                        linkedtraceId1,
                        linkedSpanId1,
                        ActivityTraceFlags.Recorded)),
                    new ActivityLink(new ActivityContext(
                        linkedtraceId2,
                        linkedSpanId2,
                        ActivityTraceFlags.Recorded)),
                };

                using var activity = source.StartActivity("SayHello", ActivityKind.Internal, parentActivity.Context, null, links);
                activity?.SetTag("http.status_code", 500); // This should be added as httpStatusCode in the mapping
                activity?.SetTag("azureResourceProvider", "Microsoft.AAD");
                activity?.SetTag("clientRequestId", "58a37988-2c05-427a-891f-5e0e1266fcc5");
                activity?.SetTag("foo", 1);
                activity?.SetTag("bar", 2);
                activity?.SetTag("activityTag", "for resource attribute conflict test");
#pragma warning disable CS0618 // Type or member is obsolete
                activity?.SetStatus(Status.Error.WithDescription("Error description from OTel API"));
#pragma warning restore CS0618 // Type or member is obsolete

                this.ExpectSpanFromActivity(activity, (mapping) =>
                    {
                        // Part A cloud extensions are checked in CheckSpanForActivity, no need to check here

                        Dictionary<object, object> userFieldsLocation;
                        if (exporterOptions.CustomFields == null)
                        {
                            userFieldsLocation = mapping;
                        }
                        else
                        {
                            Assert.Contains("env_properties", mapping.Keys);
                            userFieldsLocation = Assert.IsType<Dictionary<object, object>>(mapping["env_properties"]);
                        }

                        this.AssertMappingEntry(userFieldsLocation, "foo", 1);
                        this.AssertMappingEntry(userFieldsLocation, "bar", 2);
                        this.AssertMappingEntry(userFieldsLocation, "resourceAttribute", "resourceValue");

                        // make sure that the resource attribute value, "causes conflict", is overwritten
                        // by tag value, "for resource attribute conflict test" (tags have precedence over resource attributes)
                        this.AssertMappingEntry(userFieldsLocation, "activityTag", "for resource attribute conflict test");

                        // Linked spans are checked in CheckSpanForActivity, so no need to do a custom check here
                    });
            }

            using (var activity = source.StartActivity("TestActivityForSetStatusAPI"))
            {
                activity?.SetStatus(ActivityStatusCode.Error, description: "Error description from .NET API");

                this.ExpectSpanFromActivity(activity, (_) => { });
            }

            // If the activity Status is set using both the OTel API and the .NET API, the `Status` and `StatusDescription` set by
            // the .NET API is chosen
            using (var activity = source.StartActivity("PreferStatusFromDotnetAPI"))
            {
#pragma warning disable CS0618 // Type or member is obsolete
                activity?.SetStatus(Status.Error.WithDescription("Error description from OTel API"));
#pragma warning restore CS0618 // Type or member is obsolete
                activity?.SetStatus(ActivityStatusCode.Error, description: "Error description from .NET API");
                this.ExpectSpanFromActivity(activity, (mapping) =>
                {
                    this.AssertMappingEntry(mapping, "statusMessage", "Error description from .NET API");
                });
            }

            Assert.Equal(4, invocationCount);
        }
        finally
        {
            server?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void GenevaTraceExporter_ServerSpan_HttpUrl_Success()
    {
        var path = string.Empty;
        Socket server = null;
        try
        {
            var invocationCount = 0;
            var exporterOptions = new GenevaExporterOptions();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
            }
            else
            {
                path = GetRandomFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path;
                var endpoint = new UnixDomainSocketEndPoint(path);
                server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                server.Bind(endpoint);
                server.Listen(1);
            }

            using var exporter = new MsgPackTraceExporter(exporterOptions);

            var m_buffer = exporter.Buffer;

            // Add an ActivityListener to serialize the activity and assert that it was valid on ActivityStopped event

            // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
            // the ActivitySource used by other unit tests.
            var sourceName = GetTestMethodName();

            using var listener = new ActivityListener();
            listener.ShouldListenTo = (activitySource) => activitySource.Name == sourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = (activity) =>
            {
                _ = exporter.SerializeActivity(activity, Resource.Empty);
                var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Options);
                this.CheckSpanForActivity(exporterOptions, fluentdData, activity, exporter.DedicatedFields, []);
                invocationCount++;
            };
            ActivitySource.AddActivityListener(listener);

            var source = new ActivitySource(sourceName);

            // HTTP semconv: Combination of url.scheme, server.address, server.port, url.path and url.query
            // attributes for HTTP server spans.
            using (var parent = source.StartActivity("HttpIn", ActivityKind.Server))
            {
                parent.SetTag("http.request.method", "GET");
                parent.SetTag("url.scheme", "https");
                parent.SetTag("server.address", "localhost");
                parent.SetTag("server.port", 443);
                parent.SetTag("url.path", "/wiki/Rabbit");

                // HTTP semconv: url.full attribute for HTTP client spans.
                using (var child = source.StartActivity("HttpOut", ActivityKind.Client))
                {
                    child.SetTag("http.request.method", "GET");
                    child.SetTag("url.full", "https://www.wikipedia.org/wiki/Rabbit?id=7");
                    child.SetTag("http.status_code", 404);

                    this.ExpectSpanFromActivity(child, (mapping) =>
                    {
                        this.AssertMappingEntry(mapping, "kind", (byte)ActivityKind.Client);

                        // For HTTP client spans, they might contain this attribute for URL:
                        // Unstable HTTP semconv: http.url attribute.
                        // Stable HTTP semconv: url.full attribute.
                        // They will be mapped to httpUrl by Geneva exporter in MsgPackTraceExporter.
                        Assert.DoesNotContain("http.url", mapping.Keys);
                        Assert.DoesNotContain("url.full", mapping.Keys);

                        this.AssertMappingEntry(mapping, "httpMethod", "GET");

                        this.AssertMappingEntry(mapping, "httpUrl", "https://www.wikipedia.org/wiki/Rabbit?id=7");

                        Assert.DoesNotContain("http.status_code", mapping.Keys);
                        Assert.DoesNotContain("http.response.status_code", mapping.Keys);
                        Assert.Equal(404, Convert.ToInt32(mapping["httpStatusCode"]));
                    });
                }

                parent?.SetTag("http.response.status_code", 200);

                this.ExpectSpanFromActivity(parent, (mapping) =>
                {
                    this.AssertMappingEntry(mapping, "kind", (byte)ActivityKind.Server);

                    // For HTTP server spans, they might contain these attributes for URL:
                    // Unstable HTTP semconv: Combination of http.scheme, net.host.name, net.host.port, and http.target attributes.
                    // Stable HTTP semconv: Combination of url.scheme, server.address, server.port, url.path and url.query attributes.
                    // They will be mapped to httpUrl by Geneva exporter in MsgPackTraceExporter.
                    Assert.DoesNotContain("http.scheme", mapping.Keys);
                    Assert.DoesNotContain("net.host.name", mapping.Keys);
                    Assert.DoesNotContain("net.host.port", mapping.Keys);
                    Assert.DoesNotContain("http.target", mapping.Keys);
                    Assert.DoesNotContain("url.scheme", mapping.Keys);
                    Assert.DoesNotContain("server.address", mapping.Keys);
                    Assert.DoesNotContain("server.port", mapping.Keys);
                    Assert.DoesNotContain("url.path", mapping.Keys);
                    Assert.DoesNotContain("url.query", mapping.Keys);

                    this.AssertMappingEntry(mapping, "httpMethod", "GET");
                    this.AssertMappingEntry(mapping, "httpUrl", "https://localhost:443/wiki/Rabbit");

                    Assert.DoesNotContain("http.status_code", mapping.Keys);
                    Assert.DoesNotContain("http.response.status_code", mapping.Keys);
                    Assert.Equal(200, Convert.ToInt32(mapping["httpStatusCode"]));
                });
            }

            Assert.Equal(2, invocationCount);
        }
        finally
        {
            server?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void GenevaTraceExporter_Constructor_Missing_Agent_Linux()
    {
        var path = GetRandomFilePath();

        // System.Net.Internals.SocketExceptionFactory+ExtendedSocketException : Cannot assign requested address
        try
        {
            // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
            // the ActivitySource used by other unit tests.
            var sourceName = GetTestMethodName();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource(sourceName)
                .AddGenevaTraceExporter(options =>
                {
                    options.ConnectionString = "Endpoint=unix:" + path;
                })
                .Build();

            // GenevaExporter would not throw if it was not able to connect to the UDS socket in ctor. It would
            // keep attempting to connect to the socket when sending telemetry.
            Assert.True(true, "GenevaTraceExporter should not fail in constructor.");
        }
        catch (SocketException ex)
        {
            // There is no one to listen to the socket.
            Assert.Contains("Cannot assign requested address", ex.Message);
        }

        try
        {
            var exporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "Endpoint=unix:" + path,
            });
            Assert.True(true, "GenevaTraceExporter should not fail in constructor.");
        }
        catch (SocketException ex)
        {
            // There is no one to listent to the socket.
            Assert.Contains("Cannot assign requested address", ex.Message);
        }
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void GenevaTraceExporter_Success_Linux()
    {
        var path = GetRandomFilePath();
        try
        {
            var endpoint = new UnixDomainSocketEndPoint(path);
            using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            server.Bind(endpoint);
            server.Listen(1);

            // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
            // the ActivitySource used by other unit tests.
            var sourceName = GetTestMethodName();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource(sourceName)
                .AddGenevaTraceExporter(options =>
                {
                    options.ConnectionString = "Endpoint=unix:" + path;
                    options.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                })
                .Build();
            using var serverSocket = server.Accept();
            serverSocket.ReceiveTimeout = 10000;

            // Create a test exporter to get MessagePack byte data for validation of the data received via Socket.
            var exporter = new MsgPackTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "Endpoint=unix:" + path,
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            });

            // Emit trace and grab a copy of internal buffer for validation.
            var source = new ActivitySource(sourceName);
            var messagePackDataSize = 0;

            using (var activity = source.StartActivity("Foo", ActivityKind.Internal))
            {
                messagePackDataSize = exporter.SerializeActivity(activity, Resource.Empty).Count;
            }

            // Read the data sent via socket.
            var receivedData = new byte[1024];
            var receivedDataSize = serverSocket.Receive(receivedData);

            // Validation
            Assert.Equal(messagePackDataSize, receivedDataSize);

            // Create activity on a different thread to test for multithreading scenarios
            var thread = new Thread(() =>
            {
                using var activity = source.StartActivity("ActivityFromAnotherThread", ActivityKind.Internal);
                messagePackDataSize = exporter.SerializeActivity(activity, Resource.Empty).Count;
            });
            thread.Start();
            thread.Join();

            receivedDataSize = serverSocket.Receive(receivedData);
            Assert.Equal(messagePackDataSize, receivedDataSize);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Windows)]
    public void TLDTraceExporter_Success_Windows()
    {
        // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
        // the ActivitySource used by other unit tests.
        var sourceName = GetTestMethodName();

        // TODO: Setup a mock or spy for eventLogger to assert that eventLogger.LogInformationalEvent is actually called.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(sourceName)
            .AddGenevaTraceExporter(options =>
            {
                options.ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableTraceLoggingDynamic=true";
                options.PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.roleVer"] = "9.0.15289.2",
                };
            })
            .Build();

        var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("SayHello");
        activity?.SetTag("foo", 1);
        activity?.SetTag("bar", "Hello, World!");
        int[] expectedBaz = [1, 2, 3];
        activity?.SetTag("baz", expectedBaz);
        activity?.SetStatus(ActivityStatusCode.Ok);

        this.expectedMappingChecks[activity.OperationName] = (mapping) =>
        {
            Assert.Contains("foo", mapping.Keys);
            Assert.Equivalent(1, mapping["foo"]);

            Assert.Contains("bar", mapping.Keys);
            Assert.Equivalent("Hello, World!", mapping["bar"]);

            Assert.Contains("baz", mapping.Keys);
            Assert.Equivalent(expectedBaz, mapping["bar"]);
        };
    }

    [Fact]
    public void AddGenevaTraceExporterNamedOptionsSupport()
    {
        string connectionString;
        string connectionStringForNamedOptions;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            connectionString = "EtwSession=OpenTelemetry";
            connectionStringForNamedOptions = "EtwSession=OpenTelemetry-NamedOptions";
        }
        else
        {
            var path = GetRandomFilePath();
            connectionString = "Endpoint=unix:" + path;
            connectionStringForNamedOptions = "Endpoint=unix:" + path + "NamedOptions";
        }

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<GenevaExporterOptions>(options =>
                {
                    options.ConnectionString = connectionString;
                });
                services.Configure<GenevaExporterOptions>("ExporterWithNamedOptions", options =>
                {
                    options.ConnectionString = connectionStringForNamedOptions;
                });
            })
            .AddGenevaTraceExporter(options =>
            {
                // ConnectionString for the options is already set in `IServiceCollection Configure<TOptions>` calls above
                Assert.Equal(connectionString, options.ConnectionString);
            })
            .AddGenevaTraceExporter("ExporterWithNamedOptions", options =>
            {
                // ConnectionString for the named options is already set in `IServiceCollection Configure<TOptions>` calls above
                Assert.Equal(connectionStringForNamedOptions, options.ConnectionString);
            })
            .Build();
    }

    [Fact]
    public void AddGenevaBatchExportProcessorOptions()
    {
        var connectionString = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "EtwSession=OpenTelemetry"
            : "Endpoint=unix:" + @"C:\Users\user\AppData\Local\Temp\14tj4ac4.v2q";

        var sp = new ServiceCollection();
        sp.AddOpenTelemetry().WithTracing(builder => builder
            .ConfigureServices(services =>
            {
                services.Configure<GenevaExporterOptions>(o =>
                {
                    o.ConnectionString = connectionString;
                });
                services.Configure<BatchExportActivityProcessorOptions>(o => o.ScheduledDelayMilliseconds = 100);
            })
            .AddGenevaTraceExporter());

        var s = sp.BuildServiceProvider();

        var tracerProvider = s.GetRequiredService<TracerProvider>();

        var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var processor = typeof(BaseProcessor<Activity>)
                    .Assembly
                    .GetType("OpenTelemetry.Trace.TracerProviderSdk")
                    .GetProperty("Processor", bindingFlags)
                    .GetValue(tracerProvider) as ReentrantActivityExportProcessor;

            Assert.NotNull(processor);
        }
        else
        {
            var processor = typeof(BaseProcessor<Activity>)
                    .Assembly
                    .GetType("OpenTelemetry.Trace.TracerProviderSdk")
                    .GetProperty("Processor", bindingFlags)
                    .GetValue(tracerProvider) as BatchActivityExportProcessor;

            Assert.NotNull(processor);

            var scheduledDelayMilliseconds = typeof(BatchActivityExportProcessor)
                .GetField("ScheduledDelayMilliseconds", bindingFlags)
                .GetValue(processor);

            Assert.Equal(100, scheduledDelayMilliseconds);
        }
    }

    private static string GetRandomFilePath()
    {
        while (true)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private void CheckSpanForActivity(GenevaExporterOptions exporterOptions, object fluentdData, Activity activity, ISet<string> dedicatedFields, Dictionary<string, object> resourceAttributes)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        /* Fluentd Forward Mode:
        [
            "Span",
            [
                [ <timestamp>, { "env_ver": "4.0", ... } ]
            ],
            { "TimeFormat": "DateTime" }
        ]
        */

        var signal = (fluentdData as object[])[0] as string;
        var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
        var timeStamp = (DateTime)(TimeStampAndMappings as object[])[0];
        var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;
        var timeFormat = (fluentdData as object[])[2] as Dictionary<object, object>;

        var partAName = "Span";
        if (exporterOptions.TableNameMappings != null)
        {
            partAName = exporterOptions.TableNameMappings["Span"];
        }

        Assert.Equal(partAName, signal);

        // Timestamp check
        var dtBegin = activity.StartTimeUtc;
        var tsBegin = dtBegin.Ticks;
        var tsEnd = tsBegin + activity.Duration.Ticks;
        Assert.Equal(tsEnd, timeStamp.Ticks);

        // Part A core envelope fields

        var nameKey = MsgPackExporter.V40_PART_A_MAPPING[Schema.V40.PartA.Name];

        // Check if the user has configured a custom table mapping
        this.AssertMappingEntry(mapping, nameKey, partAName);

        // TODO: Update this when we support multiple Schema formats
        var partAVer = "4.0";
        var verKey = MsgPackExporter.V40_PART_A_MAPPING[Schema.V40.PartA.Ver];
        this.AssertMappingEntry(mapping, verKey, partAVer);

        foreach (var item in exporterOptions.PrepopulatedFields)
        {
            var partAValue = item.Value as string;
            var partAKey = MsgPackExporter.V40_PART_A_MAPPING[item.Key];
            this.AssertMappingEntry(mapping, partAKey, partAValue);
        }

        var timeKey = MsgPackExporter.V40_PART_A_MAPPING[Schema.V40.PartA.Time];
        Assert.Equal(tsEnd, ((DateTime)mapping[timeKey]).Ticks);

        // Part A cloud extensions
        if (resourceAttributes.TryGetValue("service.name", out var expectedServiceName))
        {
            this.AssertMappingEntry(mapping, "env_cloud_role", expectedServiceName);
        }

        if (resourceAttributes.TryGetValue("service.instanceId", out var expectedInstanceId))
        {
            this.AssertMappingEntry(mapping, "env_cloud_roleInstance", expectedInstanceId);
        }

        // Part A dt extensions
        this.AssertMappingEntry(mapping, "env_dt_traceId", activity.TraceId.ToString());
        this.AssertMappingEntry(mapping, "env_dt_spanId", activity.SpanId.ToString());

        // Part B Span - required fields
        this.AssertMappingEntry(mapping, "name", activity.DisplayName);
        this.AssertMappingEntry(mapping, "kind", (byte)activity.Kind);
        this.AssertMappingEntry(mapping, "startTime", activity.StartTimeUtc);

#pragma warning disable CS0618 // Type or member is obsolete
        var otelApiStatusCode = activity.GetStatus();
#pragma warning restore CS0618 // Type or member is obsolete

        if (activity.Status == ActivityStatusCode.Error)
        {
            Assert.False((bool)mapping["success"]);
            this.AssertMappingEntry(mapping, "statusMessage", activity.StatusDescription);
        }
        else if (otelApiStatusCode.StatusCode == StatusCode.Error)
        {
            Assert.False((bool)mapping["success"]);
            var activityStatusDesc = otelApiStatusCode.Description;
            this.AssertMappingEntry(mapping, "statusMessage", activityStatusDesc);
        }
        else
        {
            Assert.True((bool)mapping["success"]);
        }

        // Part B Span optional fields and Part C fields
        if (activity.ParentSpanId != default)
        {
            this.AssertMappingEntry(mapping, "parentId", activity.ParentSpanId.ToHexString());
        }

        if (!exporterOptions.IncludeTraceStateForSpan || string.IsNullOrEmpty(activity.TraceStateString))
        {
            Assert.DoesNotContain("traceState", mapping.Keys);
        }
        else
        {
            this.AssertMappingEntry(mapping, "traceState", activity.TraceStateString);
        }

        #region Assert Activity Links
        if (activity.Links.Any())
        {
            Assert.Contains(mapping, m => (m.Key as string) == "links");
            var mappingLinks = mapping["links"] as IEnumerable<object>;
            using var activityLinksEnumerator = activity.Links.GetEnumerator();
            using var mappingLinksEnumerator = mappingLinks.GetEnumerator();
            while (activityLinksEnumerator.MoveNext() && mappingLinksEnumerator.MoveNext())
            {
                var activityLink = activityLinksEnumerator.Current;
                var mappingLink = mappingLinksEnumerator.Current as Dictionary<object, object>;

                Assert.Equal(activityLink.Context.TraceId.ToHexString(), mappingLink["toTraceId"]);
                Assert.Equal(activityLink.Context.SpanId.ToHexString(), mappingLink["toSpanId"]);
            }

            // Assert that mapping contains exactly the same number of ActivityLinks as present in the activity
            // MoveNext() on both the enumerators should return false as we should have enumerated till the last element for both the Enumerables
            Assert.Equal(activityLinksEnumerator.MoveNext(), mappingLinksEnumerator.MoveNext());
        }
        else
        {
            Assert.DoesNotContain(mapping, m => (m.Key as string) == "links");
        }
        #endregion

        #region Assert Activity Tags
        _ = mapping.TryGetValue("env_properties", out var envProperties);
        var envPropertiesMapping = envProperties as IDictionary<object, object>;
        foreach (var tag in activity.TagObjects)
        {
            if (MsgPackTraceExporter.CS40_PART_B_MAPPING.TryGetValue(tag.Key, out var replacementKey))
            {
                this.AssertMappingEntry(mapping, replacementKey, tag.Value);
            }
            else if (string.Equals(tag.Key, "otel.status_code", StringComparison.Ordinal))
            {
                // Status code check is already done when we check for "success" key in the mapping
                continue;
            }
            else if (string.Equals(tag.Key, "otel.status_description", StringComparison.Ordinal))
            {
                // Status description check is already done when we check for "statusMessage" key in the mapping
                continue;
            }
            else if (MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.ContainsKey(tag.Key))
            {
                // Http url is already checked in the http span tests. Skip any http parts for now
                continue;
            }
            else
            {
                // If CustomFields are provided, dedicatedFields will be populated
                if (exporterOptions.CustomFields == null || dedicatedFields.Contains(tag.Key))
                {
                    Assert.Equal(tag.Value.ToString(), mapping[tag.Key].ToString());
                }
                else
                {
                    Assert.Equal(tag.Value.ToString(), envPropertiesMapping[tag.Key].ToString());
                }
            }
        }
        #endregion

        // Epilogue
        Assert.Equal("DateTime", timeFormat["TimeFormat"]);

        if (this.expectedMappingChecks.TryGetValue(activity.OperationName, out var value))
        {
            value?.Invoke(mapping);
            this.expectedMappingChecks.Remove(activity.OperationName);
        }
    }

    private void AssertMappingEntry(Dictionary<object, object> mapping, object key, object value)
    {
        Assert.Contains(key, mapping.Keys);
        Assert.Equivalent(value, mapping[key]);
    }

    /// <summary>
    /// Call this method to add a deferred check for an emitted span. This is useful to keep asserts near where activities are created.
    ///
    /// In order for this to work, CheckSpanForActivity must be called on the span.
    /// Also, AssertAllExpectedSpansProcessed() needs to be run at the end of the test (automatically done by Dispose method).
    /// </summary>
    /// <param name="activity">The activity sent to the exporter.</param>
    /// <param name="mappingCheck">A function which checks the fields in the resulting emitted span.</param>
    private void ExpectSpanFromActivity(Activity activity, Action<Dictionary<object, object>> mappingCheck)
    {
        Assert.DoesNotContain(activity.OperationName, this.expectedMappingChecks);
        this.expectedMappingChecks[activity.OperationName] = mappingCheck;
    }

    /// <summary>
    /// Run this when all spans are expected to have been processed.
    /// Fails assertion if any checks on spans have not been performed.
    /// </summary>
    private void AssertAllExpectedSpansProcessed()
    {
        foreach (var entry in this.expectedMappingChecks)
        {
            Assert.Fail($"Expected to see a span with name {entry.Key}, but it was never emitted");
        }
    }

    public void Dispose() => this.AssertAllExpectedSpansProcessed();
}

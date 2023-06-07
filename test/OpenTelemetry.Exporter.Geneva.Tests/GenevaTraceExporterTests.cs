// <copyright file="GenevaTraceExporterTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class GenevaTraceExporterTests
{
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
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
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

    [Fact]
    public void GenevaTraceExporter_constructor_Invalid_Input_Windows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // no ETW session name
            Assert.Throws<ArgumentOutOfRangeException>(() =>
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

    [Fact]
    public void GenevaTraceExporter_Success_Windows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
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
            using (var activity = source.StartActivity("Foo", ActivityKind.Internal, null, null, new ActivityLink[] { link }))
            {
            }

            using (var activity = source.StartActivity("Bar"))
            {
                activity.SetStatus(Status.Error);
            }

            using (var activity = source.StartActivity("Baz"))
            {
                activity.SetStatus(Status.Ok);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GenevaTraceExporter_Serialization_Success(bool hasTableNameMapping, bool hasCustomFields)
    {
        string path = string.Empty;
        Socket server = null;
        try
        {
            int invocationCount = 0;
            var exporterOptions = new GenevaExporterOptions
            {
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
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
                exporterOptions.CustomFields = new string[] { "clientRequestId" };
            }

            using var exporter = new MsgPackTraceExporter(exporterOptions);
            var dedicatedFields = typeof(MsgPackTraceExporter).GetField("m_dedicatedFields", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as IReadOnlyDictionary<string, object>;
            var CS40_PART_B_MAPPING = typeof(MsgPackTraceExporter).GetField("CS40_PART_B_MAPPING", BindingFlags.NonPublic | BindingFlags.Static).GetValue(exporter) as IReadOnlyDictionary<string, string>;
            var m_buffer = typeof(MsgPackTraceExporter).GetField("m_buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as ThreadLocal<byte[]>;

            // Add an ActivityListener to serialize the activity and assert that it was valid on ActivityStopped event

            // Set the ActivitySourceName to the unique value of the test method name to avoid interference with
            // the ActivitySource used by other unit tests.
            var sourceName = GetTestMethodName();
            Action<Dictionary<object, object>> customChecksForActivity = null;

            using var listener = new ActivityListener();
            listener.ShouldListenTo = (activitySource) => activitySource.Name == sourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = (activity) =>
            {
                _ = exporter.SerializeActivity(activity);
                object fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(m_buffer.Value, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
                this.AssertFluentdForwardModeForActivity(exporterOptions, fluentdData, activity, CS40_PART_B_MAPPING, dedicatedFields, customChecksForActivity);
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

                using (var activity = source.StartActivity("SayHello", ActivityKind.Internal, parentActivity.Context, null, links))
                {
                    activity?.SetTag("http.status_code", 500); // This should be added as httpStatusCode in the mapping
                    activity?.SetTag("azureResourceProvider", "Microsoft.AAD");
                    activity?.SetTag("clientRequestId", "58a37988-2c05-427a-891f-5e0e1266fcc5");
                    activity?.SetTag("foo", 1);
                    activity?.SetTag("bar", 2);
                    activity?.SetStatus(Status.Error.WithDescription("Error description from OTel API"));
                }
            }

            using (var activity = source.StartActivity("TestActivityForSetStatusAPI"))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Error description from .NET API");
            }

            // If the activity Status is set using both the OTel API and the .NET API, the `Status` and `StatusDescription` set by
            // the .NET API is chosen
            using (var activity = source.StartActivity("PreferStatusFromDotnetAPI"))
            {
                activity?.SetStatus(Status.Error.WithDescription("Error description from OTel API"));
                activity?.SetStatus(ActivityStatusCode.Error, "Error description from .NET API");
                customChecksForActivity = mapping =>
                {
                    Assert.Equal("Error description from .NET API", mapping["statusMessage"]);
                };
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
    public void GenevaTraceExporter_Constructor_Missing_Agent_Linux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string path = GetRandomFilePath();

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
                // There is no one to listent to the socket.
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
    }

    [Fact]
    public void GenevaTraceExporter_Success_Linux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string path = GetRandomFilePath();
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
                            ["cloud.role"] = "BusyWorker",
                            ["cloud.roleInstance"] = "CY1SCH030021417",
                            ["cloud.roleVer"] = "9.0.15289.2",
                        };
                    })
                    .Build();
                using Socket serverSocket = server.Accept();
                serverSocket.ReceiveTimeout = 10000;

                // Create a test exporter to get MessagePack byte data for validation of the data received via Socket.
                var exporter = new MsgPackTraceExporter(new GenevaExporterOptions
                {
                    ConnectionString = "Endpoint=unix:" + path,
                    PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    },
                });

                // Emit trace and grab a copy of internal buffer for validation.
                var source = new ActivitySource(sourceName);
                int messagePackDataSize = 0;

                using (var activity = source.StartActivity("Foo", ActivityKind.Internal))
                {
                    messagePackDataSize = exporter.SerializeActivity(activity);
                }

                // Read the data sent via socket.
                var receivedData = new byte[1024];
                int receivedDataSize = serverSocket.Receive(receivedData);

                // Validation
                Assert.Equal(messagePackDataSize, receivedDataSize);

                // Create activity on a different thread to test for multithreading scenarios
                var thread = new Thread(() =>
                {
                    using (var activity = source.StartActivity("ActivityFromAnotherThread", ActivityKind.Internal))
                    {
                        messagePackDataSize = exporter.SerializeActivity(activity);
                    }
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
    }

    [Fact]
    public void TLDTraceExporter_Success_Windows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                })
                .Build();

            var source = new ActivitySource(sourceName);
            using (var activity = source.StartActivity("SayHello"))
            {
                activity?.SetTag("foo", 1);
                activity?.SetTag("bar", "Hello, World!");
                activity?.SetTag("baz", new int[] { 1, 2, 3 });
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
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

    private static string GetRandomFilePath()
    {
        while (true)
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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

    private void AssertFluentdForwardModeForActivity(GenevaExporterOptions exporterOptions, object fluentdData, Activity activity, IReadOnlyDictionary<string, string> CS40_PART_B_MAPPING, IReadOnlyDictionary<string, object> dedicatedFields, Action<Dictionary<object, object>> customChecksForActivity)
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
        Assert.Equal(partAName, mapping[nameKey]);

        // TODO: Update this when we support multiple Schema formats
        var partAVer = "4.0";
        var verKey = MsgPackExporter.V40_PART_A_MAPPING[Schema.V40.PartA.Ver];
        Assert.Equal(partAVer, mapping[verKey]);

        foreach (var item in exporterOptions.PrepopulatedFields)
        {
            var partAValue = item.Value as string;
            var partAKey = MsgPackExporter.V40_PART_A_MAPPING[item.Key];
            Assert.Equal(partAValue, mapping[partAKey]);
        }

        var timeKey = MsgPackExporter.V40_PART_A_MAPPING[Schema.V40.PartA.Time];
        Assert.Equal(tsEnd, ((DateTime)mapping[timeKey]).Ticks);

        // Part A dt extensions
        Assert.Equal(activity.TraceId.ToString(), mapping["env_dt_traceId"]);
        Assert.Equal(activity.SpanId.ToString(), mapping["env_dt_spanId"]);

        // Part B Span - required fields
        Assert.Equal(activity.DisplayName, mapping["name"]);
        Assert.Equal((byte)activity.Kind, mapping["kind"]);
        Assert.Equal(activity.StartTimeUtc, mapping["startTime"]);

        var activityStatusCode = activity.GetStatus().StatusCode;

        if (activity.Status == ActivityStatusCode.Error)
        {
            Assert.False((bool)mapping["success"]);
            Assert.Equal(activity.StatusDescription, mapping["statusMessage"]);
        }
        else if (activityStatusCode == StatusCode.Error)
        {
            Assert.False((bool)mapping["success"]);
            var activityStatusDesc = activity.GetStatus().Description;
            Assert.Equal(activityStatusDesc, mapping["statusMessage"]);
        }
        else
        {
            Assert.True((bool)mapping["success"]);
        }

        // Part B Span optional fields and Part C fields
        if (activity.ParentSpanId != default)
        {
            Assert.Equal(activity.ParentSpanId.ToHexString(), mapping["parentId"]);
        }

        #region Assert Activity Links
        if (activity.Links.Any())
        {
            Assert.Contains(mapping, m => m.Key as string == "links");
            var mappingLinks = mapping["links"] as IEnumerable<object>;
            using IEnumerator<ActivityLink> activityLinksEnumerator = activity.Links.GetEnumerator();
            using IEnumerator<object> mappingLinksEnumerator = mappingLinks.GetEnumerator();
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
            Assert.DoesNotContain(mapping, m => m.Key as string == "links");
        }
        #endregion

        #region Assert Activity Tags
        _ = mapping.TryGetValue("env_properties", out object envProprties);
        var envPropertiesMapping = envProprties as IDictionary<object, object>;
        foreach (var tag in activity.TagObjects)
        {
            if (CS40_PART_B_MAPPING.TryGetValue(tag.Key, out string replacementKey))
            {
                Assert.Equal(tag.Value.ToString(), mapping[replacementKey].ToString());
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
            else
            {
                // If CustomFields are proivded, dedicatedFields will be populated
                if (exporterOptions.CustomFields == null || dedicatedFields.TryGetValue(tag.Key, out _))
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

        // Epilouge
        Assert.Equal("DateTime", timeFormat["TimeFormat"]);

        customChecksForActivity?.Invoke(mapping);
    }
}

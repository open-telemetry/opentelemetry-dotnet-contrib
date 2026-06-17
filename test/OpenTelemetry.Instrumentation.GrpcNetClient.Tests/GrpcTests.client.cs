// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Greet;
#if !NETFRAMEWORK
using Grpc.Core;
#endif
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
#if !NETFRAMEWORK
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Tests;
#endif
using OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using RpcException = Grpc.Core.RpcException;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

public partial class GrpcTests
{
    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost", false)]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1", false)]
    [InlineData("http://[::1]")]
    [InlineData("http://[::1]", false)]
    public void GrpcClientCallsAreCollectedSuccessfully(string baseAddress, bool shouldEnrich = true)
    {
        var enrichWithHttpRequestMessageCalled = false;
        var enrichWithHttpResponseMessageCalled = false;

        var uri = new UriBuilder(baseAddress) { Port = 1234 }.Uri;

        using var httpClient = ClientTestHelpers.CreateTestClient(async request =>
        {
            var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply());
            var response = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.OK);
            response.TrailingHeaders().Add("grpc-message", "value");
            return response;
        });

        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(options =>
                {
                    if (shouldEnrich)
                    {
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build())
        {
            var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
            {
                HttpClient = httpClient,
            });
            var client = new Greeter.GreeterClient(channel);
            _ = client.SayHello(new HelloRequest());
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        ValidateGrpcActivity(activity);
        Assert.Equal(parent.TraceId, activity.Context.TraceId);
        Assert.Equal(parent.SpanId, activity.ParentSpanId);
        Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
        Assert.NotEqual(default, activity.Context.SpanId);

        Assert.Equal($"greet.Greeter/SayHello", activity.DisplayName);
        Assert.Equal("grpc", activity.GetTagValue(SemanticConventions.AttributeRpcSystemName));
        Assert.Equal("greet.Greeter/SayHello", activity.GetTagValue(SemanticConventions.AttributeRpcMethod));

        Assert.Equal(uri.Host, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(uri.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);

        // Tags added by the library then removed from the instrumentation
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcStatusCodeTagName));
        Assert.Equal("OK", activity.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));

        if (shouldEnrich)
        {
            Assert.True(enrichWithHttpRequestMessageCalled);
            Assert.True(enrichWithHttpResponseMessageCalled);
        }
    }

    [Fact]
    public void GrpcClientCancelledCallIsRecordedAsErrorPerSemanticConventions()
    {
        // The gRPC semantic conventions specify that, for client spans, all status codes
        // other than OK are errors. A cancelled call (status code CANCELLED) therefore has
        // its span status set to Error and the error.type attribute set to the status code name.
        // See https://github.com/open-telemetry/semantic-conventions/blob/v1.42.0/docs/rpc/grpc.md
        var uri = new UriBuilder("http://localhost") { Port = 1234 }.Uri;

        using var httpClient = ClientTestHelpers.CreateTestClient(async request =>
        {
            var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply());
            return ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.Cancelled);
        });

        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                  .SetSampler(new AlwaysOnSampler())
                  .AddGrpcClientInstrumentation()
                  .AddInMemoryExporter(exportedItems)
                  .Build())
        {
            var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
            {
                HttpClient = httpClient,
            });
            var client = new Greeter.GreeterClient(channel);
            Assert.Throws<RpcException>(() => client.SayHello(new HelloRequest()));
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal("CANCELLED", activity.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("CANCELLED", activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void GrpcClientCancellationCanBeTreatedAsNonErrorWithCustomProcessor()
    {
        // The gRPC semantic conventions classify all non-OK client status codes as errors.
        // An application that deliberately cancels a call has additional context and MAY
        // override the span status to not report the cancellation as an error. This test
        // demonstrates how to do that using a custom processor.
        //
        // Note that the EnrichWithHttpResponseMessage option cannot be used for this purpose
        // because it is not invoked when a call is cancelled (there is no response message).
        // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/2066
        var uri = new UriBuilder("http://localhost") { Port = 1234 }.Uri;

        using var httpClient = ClientTestHelpers.CreateTestClient(async request =>
        {
            var streamContent = await ClientTestHelpers.CreateResponseContent(new HelloReply());
            return ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: global::Grpc.Core.StatusCode.Cancelled);
        });

        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                  .SetSampler(new AlwaysOnSampler())
                  .AddGrpcClientInstrumentation()
                  .AddProcessor(new CancelledGrpcCallStatusProcessor())
                  .AddInMemoryExporter(exportedItems)
                  .Build())
        {
            var channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
            {
                HttpClient = httpClient,
            });
            var client = new Greeter.GreeterClient(channel);
            Assert.Throws<RpcException>(() => client.SayHello(new HelloRequest()));
        }

        var activity = Assert.Single(exportedItems);

        // The status code attribute is still recorded as required by the semantic conventions...
        Assert.Equal("CANCELLED", activity.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));

        // ...but the application's processor has cleared the error signal.
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

#if NET
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GrpcAndHttpClientInstrumentationIsInvoked(bool shouldEnrich)
    {
        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(options =>
                {
                    if (shouldEnrich)
                    {
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpRequestMessage", "yes");
                        };

                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpResponseMessage", "yes");
                        };
                    }
                })
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build())
        {
            using var channel = GrpcChannel.ForAddress(this.server.Address, new GrpcChannelOptions()
            {
                HttpClient = new HttpClient(),
            });

            var client = new Greeter.GreeterClient(channel);
            _ = client.SayHello(new HelloRequest());
        }

        Assert.Equal(2, exportedItems.Count);
        var httpSpan = exportedItems.Single(activity => activity.OperationName == OperationNameHttpOut);
        var grpcSpan = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

        ValidateGrpcActivity(grpcSpan);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan.DisplayName);
        Assert.Equal("OK", grpcSpan.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));
        Assert.Equal("POST", httpSpan.DisplayName);
        Assert.Equal(grpcSpan.SpanId, httpSpan.ParentSpanId);

        if (shouldEnrich)
        {
            Assert.Single(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpRequestMessage" && tag.Value == "yes");
            Assert.Single(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpResponseMessage" && tag.Value == "yes");
        }
        else
        {
            Assert.DoesNotContain(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpRequestMessage");
            Assert.DoesNotContain(grpcSpan.Tags, tag => tag.Key == "enrichedWithHttpResponseMessage");
        }
    }

    [Fact]
    public void GrpcAndHttpClientInstrumentationWithSuppressInstrumentation()
    {
        var exportedItems = new List<Activity>();

        using var parent = new Activity("parent")
            .Start();

        using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddGrpcClientInstrumentation(o => o.SuppressDownstreamInstrumentation = true)
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build())
        {
            Parallel.ForEach(
            new int[4],
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
            },
            (value) =>
            {
                using var channel = GrpcChannel.ForAddress(this.server.Address);
                var client = new Greeter.GreeterClient(channel);
                _ = client.SayHello(new HelloRequest());
            });
        }

        Assert.Equal(4, exportedItems.Count);
        var grpcSpan1 = exportedItems[0];
        var grpcSpan2 = exportedItems[1];
        var grpcSpan3 = exportedItems[2];
        var grpcSpan4 = exportedItems[3];

        ValidateGrpcActivity(grpcSpan1);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan1.DisplayName);
        Assert.Equal("OK", grpcSpan1.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));

        ValidateGrpcActivity(grpcSpan2);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan2.DisplayName);
        Assert.Equal("OK", grpcSpan2.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));

        ValidateGrpcActivity(grpcSpan3);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan3.DisplayName);
        Assert.Equal("OK", grpcSpan3.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));

        ValidateGrpcActivity(grpcSpan4);
        Assert.Equal($"greet.Greeter/SayHello", grpcSpan4.DisplayName);
        Assert.Equal("OK", grpcSpan4.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));
    }

#if NET
    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1727")]
#else
    [Fact]
#endif
    public void GrpcPropagatesContextWithSuppressInstrumentationOptionSetToTrue()
    {
        try
        {
            var exportedItems = new List<Activity>();

            using var source = new ActivitySource("test-source");

            var propagator = new CustomTextMapPropagator();
            propagator.InjectValues.Add("customField", context => "customValue");

            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
                new TraceContextPropagator(),
                propagator
            ]));

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o =>
                {
                    o.SuppressDownstreamInstrumentation = true;
                })
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetCustomProperty("customField", request.Headers["customField"].ToString());
                    };
                }) // Instrumenting the server side as well
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                Assert.NotNull(activity);

                using var channel = GrpcChannel.ForAddress(this.server.Address);
                var client = new Greeter.GreeterClient(channel);
                _ = client.SayHello(new HelloRequest());
            }

            var serverActivity = exportedItems.Single(activity => activity.OperationName == OperationNameHttpRequestIn);
            var clientActivity = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

            Assert.Equal($"greet.Greeter/SayHello", clientActivity.DisplayName);
            Assert.Equal($"POST /greet.Greeter/SayHello", serverActivity.DisplayName);
            Assert.Equal(clientActivity.TraceId, serverActivity.TraceId);
            Assert.Equal(clientActivity.SpanId, serverActivity.ParentSpanId);
            Assert.Equal("OK", clientActivity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
            Assert.Equal("customValue", serverActivity.GetCustomProperty("customField") as string);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
                new TraceContextPropagator(),
                new BaggagePropagator()
            ]));
        }
    }

    [Fact]
    public void GrpcDoesNotPropagateContextWithSuppressInstrumentationOptionSetToFalse()
    {
        try
        {
            var exportedItems = new List<Activity>();
            using var source = new ActivitySource("test-source");

            var isPropagatorCalled = false;
            var propagator = new CustomTextMapPropagator
            {
                Injected = (context) => isPropagatorCalled = true,
            };

            Sdk.SetDefaultTextMapPropagator(propagator);

            var headers = new Metadata();

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o =>
                {
                    o.SuppressDownstreamInstrumentation = false;
                })
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                using var channel = GrpcChannel.ForAddress(this.server.Address);
                var client = new Greeter.GreeterClient(channel);
                _ = client.SayHello(new HelloRequest(), headers);
            }

            Assert.Equal(2, exportedItems.Count);

            var parentActivity = exportedItems.Single(activity => activity.OperationName == "parent");
            var clientActivity = exportedItems.Single(activity => activity.OperationName == OperationNameGrpcOut);

            Assert.Equal(clientActivity.ParentSpanId, parentActivity.SpanId);
            Assert.False(isPropagatorCalled, "Propagator was called.");
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
                new TraceContextPropagator(),
                new BaggagePropagator()
            ]));
        }
    }

    [Fact]
    public void GrpcClientInstrumentationRespectsSdkSuppressInstrumentation()
    {
        try
        {
            var exportedItems = new List<Activity>();

            using var source = new ActivitySource("test-source");

            var isPropagatorCalled = false;
            var propagator = new CustomTextMapPropagator { Injected = _ => isPropagatorCalled = true };

            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
                new TraceContextPropagator(),
                propagator
            ]));

            using (Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddGrpcClientInstrumentation(o => o.SuppressDownstreamInstrumentation = true)
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var activity = source.StartActivity("parent");
                using (SuppressInstrumentationScope.Begin())
                {
                    using var channel = GrpcChannel.ForAddress(this.server.Address);
                    var client = new Greeter.GreeterClient(channel);
                    _ = client.SayHello(new HelloRequest());
                }
            }

            // If suppressed, activity is not emitted and
            // propagation is also not performed.
            Assert.Single(exportedItems);
            Assert.False(isPropagatorCalled, "Propagator was called.");
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
                new TraceContextPropagator(),
                new BaggagePropagator()
            ]));
        }
    }
#endif

    [Fact]
    public void AddGrpcClientInstrumentationNamedOptionsSupported()
    {
        var defaultExporterOptionsConfigureOptionsInvocations = 0;
        var namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<GrpcClientTraceInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<GrpcClientTraceInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddGrpcClientInstrumentation()
            .AddGrpcClientInstrumentation("Instrumentation2", configure: null)
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void Grpc_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddGrpcClientInstrumentation());
    }

    private static void ValidateGrpcActivity(Activity activityToValidate)
    {
        Assert.Equal("OpenTelemetry.Instrumentation.GrpcNetClient", activityToValidate.Source.Name);
        Assert.NotNull(activityToValidate.Source.Version);
        Assert.NotEmpty(activityToValidate.Source.Version);
        Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
        Assert.StartsWith("https://opentelemetry.io/schemas/", activityToValidate.Source.TelemetrySchemaUrl);
    }

    private sealed class CancelledGrpcCallStatusProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // The application knows a CANCELLED result is the result of a deliberate
            // cancellation rather than a failure, so the error signal is cleared while
            // leaving the rpc.response.status_code attribute in place.
            if ((activity.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode) as string) == "CANCELLED")
            {
                activity.SetStatus(ActivityStatusCode.Unset);
                activity.SetTag(SemanticConventions.AttributeErrorType, null);
            }
        }
    }
}

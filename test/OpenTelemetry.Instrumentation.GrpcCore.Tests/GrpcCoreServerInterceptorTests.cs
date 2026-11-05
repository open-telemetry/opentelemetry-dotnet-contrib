// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Grpc.Core;
using Grpc.Core.Interceptors;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

/// <summary>
/// Grpc Core server interceptor tests.
/// </summary>
public class GrpcCoreServerInterceptorTests(WeaverFixture weaver, ITestOutputHelper outputHelper)
    : IClassFixture<WeaverFixture>
{
    /// <summary>
    /// Validates a successful UnaryServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnaryServerHandlerSuccess() =>
        await this.TestHandlerSuccess(FoobarService.MakeUnaryAsyncRequest, FoobarService.UnaryMethod);

    /// <summary>
    /// Validates a failed UnaryServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnaryServerHandlerFail() =>
        await TestHandlerFailure(FoobarService.MakeUnaryAsyncRequest, FoobarService.UnaryMethod);

    /// <summary>
    /// Validates a successful ClientStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingServerHandlerSuccess() =>
        await this.TestHandlerSuccess(FoobarService.MakeClientStreamingRequest, FoobarService.ClientStreamingMethod);

    /// <summary>
    /// Validates a failed ClientStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingServerHandlerFail() =>
        await TestHandlerFailure(FoobarService.MakeClientStreamingRequest, FoobarService.ClientStreamingMethod);

    /// <summary>
    /// Validates a successful ServerStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ServerStreamingServerHandlerSuccess() =>
        await this.TestHandlerSuccess(FoobarService.MakeServerStreamingRequest, FoobarService.ServerStreamingMethod);

    /// <summary>
    /// Validates a failed ServerStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ServerStreamingServerHandlerFail() =>
        await TestHandlerFailure(FoobarService.MakeServerStreamingRequest, FoobarService.ServerStreamingMethod);

    /// <summary>
    /// Validates a successful DuplexStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DuplexStreamingServerHandlerSuccess() =>
        await this.TestHandlerSuccess(FoobarService.MakeDuplexStreamingRequest, FoobarService.DuplexStreamingMethod);

    /// <summary>
    /// Validates a failed DuplexStreamingServerHandler call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DuplexStreamingServerHandlerFail() =>
        await TestHandlerFailure(FoobarService.MakeDuplexStreamingRequest, FoobarService.DuplexStreamingMethod);

    /// <summary>
    /// Validates that non protobuf payloads do not abort server RPCs when
    /// message event recording is enabled.
    /// </summary>
    [Fact]
    public async Task UnaryServerHandlerWithNonProtobufPayloadDoesNotThrowWhenRecordingMessageEvents()
    {
        var testTags = new TestActivityTags();
        var interceptorOptions = new ServerTracingInterceptorOptions
        {
            Propagator = new TraceContextPropagator(),
            RecordMessageEvents = true,
            AdditionalTags = testTags.Tags,
        };

        static Task<NonProtobufPayload> HandleUnaryCall(NonProtobufPayload request, ServerCallContext context)
        {
            return Task.FromResult(new NonProtobufPayload());
        }

        var serviceDefinition = ServerServiceDefinition.CreateBuilder()
            .AddMethod(
                NonProtobufGrpcTestHelpers.UnaryMethod,
                HandleUnaryCall)
            .Build()
            .Intercept(new ServerTracingInterceptor(interceptorOptions));

        var server = new Server
        {
            Ports = { { "localhost", ServerPort.PickUnused, ServerCredentials.Insecure } },
            Services = { serviceDefinition },
        };

        server.Start();
        var serverUriString = $"dns:localhost:{server.Ports.Single().BoundPort}";

        try
        {
            using var activityListener = new InterceptorActivityListener(testTags);
            var channel = new Channel(serverUriString, ChannelCredentials.Insecure);

            try
            {
                var response = channel.CreateCallInvoker().BlockingUnaryCall(
                    NonProtobufGrpcTestHelpers.UnaryMethod,
                    null,
                    default,
                    new NonProtobufPayload());

                Assert.NotNull(response);

                var activity = activityListener.Activity;

                GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(activity, FoobarService.UnaryMethod);

                Assert.Empty(activity!.Events);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }
        finally
        {
            await server.ShutdownAsync();
        }
    }

    /// <summary>
    /// Validates that baggage extracted from the request headers is only
    /// visible to the RPC it was sent with and does not overwrite the baggage
    /// of the execution context the RPC was started in, which is shared by
    /// every RPC when a baggage holder exists there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnaryServerHandlerDoesNotWriteExtractedBaggageIntoInheritedBaggageHolder()
    {
        // Start-up code which sets and clears baggage leaves an empty baggage
        // holder behind in the execution context the RPCs are started in.
        Baggage.SetBaggage("warm-up", "true");
        Baggage.ClearBaggage();

        var testTags = new TestActivityTags();
        var interceptorOptions = new ServerTracingInterceptorOptions
        {
            Propagator = new BaggagePropagator(),
            AdditionalTags = testTags.Tags,
        };

        var interceptor = new ServerTracingInterceptor(interceptorOptions);
        using var activityListener = new InterceptorActivityListener(testTags);

        string? tenantSeenByHandler = null;
        var response = await interceptor.UnaryServerHandler(
            FoobarService.DefaultRequestMessage,
            new TestServerCallContext([new Metadata.Entry("baggage", "tenant=from-the-caller")]),
            (request, context) =>
            {
                tenantSeenByHandler = Baggage.GetBaggage("tenant");
                return Task.FromResult(FoobarService.DefaultResponseMessage);
            });

        Assert.Same(FoobarService.DefaultResponseMessage, response);
        Assert.NotNull(activityListener.Activity);
        Assert.Equal("from-the-caller", tenantSeenByHandler);
        Assert.Null(Baggage.GetBaggage("tenant"));
    }

    /// <summary>
    /// Validates that baggage sent with one RPC is not visible to an RPC that
    /// is already in flight, or to RPCs that start afterwards, when a baggage
    /// holder exists in the execution context the RPCs are started in.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task UnaryServerHandlerDoesNotShareBaggageBetweenRpcs()
    {
        Baggage.SetBaggage("warm-up", "true");
        Baggage.ClearBaggage();

        var testTags = new TestActivityTags();
        var interceptor = new ServerTracingInterceptor(new ServerTracingInterceptorOptions
        {
            Propagator = new BaggagePropagator(),
            AdditionalTags = testTags.Tags,
        });

        using var activityListener = new InterceptorActivityListener(testTags);

        string? tenantSeenByLaterRpc = null;
        string? tenantSeenByInFlightRpcBefore = null;
        string? tenantSeenByInFlightRpcAfter = null;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // An RPC without baggage that is still running while the others are handled.
        var inFlight = interceptor.UnaryServerHandler(
            FoobarService.DefaultRequestMessage,
            new TestServerCallContext([]),
            async (request, context) =>
            {
                tenantSeenByInFlightRpcBefore = Baggage.GetBaggage("tenant");
                await release.Task;
                tenantSeenByInFlightRpcAfter = Baggage.GetBaggage("tenant");
                return FoobarService.DefaultResponseMessage;
            });

        await interceptor.UnaryServerHandler(
            FoobarService.DefaultRequestMessage,
            new TestServerCallContext([new Metadata.Entry("baggage", "tenant=attacker")]),
            (request, context) => Task.FromResult(FoobarService.DefaultResponseMessage));

        await interceptor.UnaryServerHandler(
            FoobarService.DefaultRequestMessage,
            new TestServerCallContext([new Metadata.Entry("baggage", "tenant=customer")]),
            (request, context) =>
            {
                tenantSeenByLaterRpc = Baggage.GetBaggage("tenant");
                return Task.FromResult(FoobarService.DefaultResponseMessage);
            });

        release.SetResult(true);
        await inFlight;

        Assert.Null(tenantSeenByInFlightRpcBefore);
        Assert.Null(tenantSeenByInFlightRpcAfter);
        Assert.Equal("customer", tenantSeenByLaterRpc);
    }

    /// <summary>
    /// Validates that the number of message events recorded for a streaming call
    /// is bounded, regardless of how many messages the client sends.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingServerHandlerLimitsRecordedMessageEvents()
    {
        var testTags = new TestActivityTags();
        var interceptorOptions = new ServerTracingInterceptorOptions
        {
            Propagator = new TraceContextPropagator(),
            RecordMessageEvents = true,
            AdditionalTags = testTags.Tags,
        };

        using var server = FoobarService.Start(new ServerTracingInterceptor(interceptorOptions));
        using var activityListener = new InterceptorActivityListener(testTags);

        using var eventListener = new InMemoryEventListener(GrpcCoreInstrumentationEventSource.Log, EventLevel.Warning);

        var client = FoobarService.ConstructRpcClient(server.Target);
        var messageCount = RpcScope<FoobarRequest, FoobarResponse>.MaxMessageEvents * 2;

        using (var call = client.ClientStreaming(cancellationToken: TestContext.Current.CancellationToken))
        {
            for (var i = 0; i < messageCount; i++)
            {
#if NET
                await call.RequestStream.WriteAsync(FoobarService.DefaultRequestMessage, CancellationToken.None);
#else
                await call.RequestStream.WriteAsync(FoobarService.DefaultRequestMessage);
#endif
            }

            await call.RequestStream.CompleteAsync();
            _ = await call.ResponseAsync;
        }

        var activity = activityListener.Activity;

        GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(activity, FoobarService.ClientStreamingMethod);

        // Only the first MaxMessageEvents messages are recorded, the rest are dropped.
        Assert.NotNull(activity);
        Assert.Equal(RpcScope<FoobarRequest, FoobarResponse>.MaxMessageEvents, activity.Events.Count());
        Assert.All(activity.Events, e => Assert.Equal(nameof(FoobarRequest), e.Name));

        // A single warning is logged however many messages are dropped. The event source is
        // shared, so ignore warnings logged for other calls by tests running in parallel.
        var logged = Assert.Single(
            eventListener.Events,
            e => e.EventId == 1 && ((string?)e.Payload?[0])?.EndsWith(FoobarService.ClientStreamingMethod, StringComparison.Ordinal) == true);
        Assert.Equal(EventLevel.Warning, logged.Level);
        Assert.Equal(RpcScope<FoobarRequest, FoobarResponse>.MaxMessageEvents, (int)logged.Payload![1]!);
    }

    /// <summary>
    /// A common method to test server interceptor handler failure.
    /// </summary>
    /// <param name="clientRequestFunc">The specific client request function.</param>
    /// <param name="expectedMethodName">The expected gRPC method name.</param>
    /// <param name="additionalMetadata">The additional metadata, if any.</param>
    /// <returns>A Task.</returns>
    private static async Task TestHandlerFailure(
        Func<Foobar.FoobarClient, Metadata?, Task> clientRequestFunc,
        string expectedMethodName,
        Metadata? additionalMetadata = null)
    {
        // starts the server with the server interceptor
        var testTags = new TestActivityTags();
        var interceptorOptions = new ServerTracingInterceptorOptions { Propagator = new TraceContextPropagator(), AdditionalTags = testTags.Tags, RecordException = true };
        using var server = FoobarService.Start(new ServerTracingInterceptor(interceptorOptions));

        using var activityListener = new InterceptorActivityListener(testTags);
        var client = FoobarService.ConstructRpcClient(
            server.Target,
            additionalMetadata:
            [
                new Metadata.Entry("traceparent", FoobarService.DefaultTraceparentWithSampling),
                new Metadata.Entry(FoobarService.RequestHeaderFailWithStatusCode, StatusCode.ResourceExhausted.ToString()),
                new Metadata.Entry(FoobarService.RequestHeaderErrorDescription, "fubar"),
            ]);

        await Assert.ThrowsAsync<RpcException>(async () => await clientRequestFunc(client, additionalMetadata).ConfigureAwait(false));

        var activity = activityListener.Activity;

        GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(
            activity,
            expectedMethodName,
            StatusCode.ResourceExhausted,
            interceptorOptions.RecordMessageEvents,
            interceptorOptions.RecordException);

        Assert.NotNull(activity);
        Assert.Equal(FoobarService.DefaultParentFromTraceparentHeader.SpanId, activity.ParentSpanId);
    }

    /// <summary>
    /// A common method to test server interceptor handler success.
    /// </summary>
    /// <param name="clientRequestFunc">The specific client request function.</param>
    /// <param name="expectedMethodName">The expected gRPC method name.</param>
    /// <param name="additionalMetadata">The additional metadata, if any.</param>
    /// <returns>A Task.</returns>
    private async Task TestHandlerSuccess(
        Func<Foobar.FoobarClient, Metadata?, Task> clientRequestFunc,
        string expectedMethodName,
        Metadata? additionalMetadata = null)
    {
        // starts the server with the server interceptor
        var testTags = new TestActivityTags();
        var interceptorOptions = new ServerTracingInterceptorOptions
        {
            Propagator = new TraceContextPropagator(),
            RecordMessageEvents = true,
            AdditionalTags = testTags.Tags,
        };

        using var server = FoobarService.Start(new ServerTracingInterceptor(interceptorOptions));

        // No parent Activity, no context from header
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            var client = FoobarService.ConstructRpcClient(server.Target);
            await clientRequestFunc(client, additionalMetadata);

            var activity = activityListener.Activity;

            GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(
                activity,
                expectedMethodName,
                StatusCode.OK,
                interceptorOptions.RecordMessageEvents);

            Assert.NotNull(activity);
            Assert.Equal(default, activity.ParentSpanId);
        }

        // No parent Activity, context from header
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            var client = FoobarService.ConstructRpcClient(
                server.Target,
                additionalMetadata: [new Metadata.Entry("traceparent", FoobarService.DefaultTraceparentWithSampling)]);

            await clientRequestFunc(client, additionalMetadata).ConfigureAwait(false);

            var activity = activityListener.Activity;

            GrpcCoreClientInterceptorTests.ValidateCommonActivityTags(
                activity,
                expectedMethodName,
                StatusCode.OK,
                interceptorOptions.RecordMessageEvents);

            Assert.NotNull(activity);
            Assert.Equal(FoobarService.DefaultParentFromTraceparentHeader.SpanId, activity.ParentSpanId);

            if (DockerHelper.IsAvailable(DockerPlatform.Linux))
            {
                await WeaverTelemetryVerifier.VerifyAsync(
                    ([activity], []),
                    GrpcCoreInstrumentation.SemanticConventionsVersion,
                    weaver,
                    outputHelper,
                    [new("missing_attribute", "Attribute 'activityidentifier' does not exist in the registry.")]);
            }
        }
    }
}

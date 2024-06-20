// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using StatusCode = Grpc.Core.StatusCode;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

/// <summary>
/// Grpc Core client interceptor tests.
/// </summary>
public class GrpcCoreClientInterceptorTests
{
    /// <summary>
    /// A bogus server uri.
    /// </summary>
    private const string BogusServerUri = "dns:i.dont.exist:77923";

    /// <summary>
    /// The default metadata func.
    /// </summary>
    private static readonly Func<Metadata> DefaultMetadataFunc = () => new Metadata { new Metadata.Entry("foo", "bar") };

    /// <summary>
    /// Validates a successful AsyncUnary call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AsyncUnarySuccess()
    {
        await TestHandlerSuccess(FoobarService.MakeUnaryAsyncRequest, DefaultMetadataFunc());
    }

    /// <summary>
    /// Validates a failed AsyncUnary call because the endpoint isn't there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AsyncUnaryUnavailable()
    {
        await TestHandlerFailure(
            FoobarService.MakeUnaryAsyncRequest,
            StatusCode.Unavailable,
            validateErrorDescription: false,
            BogusServerUri);
    }

    /// <summary>
    /// Validates a failed AsyncUnary call because the service returned an error.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AsyncUnaryFail()
    {
        await TestHandlerFailure(FoobarService.MakeUnaryAsyncRequest);
    }

    /// <summary>
    /// Validates a failed AsyncUnary call because the client is disposed before completing the RPC.
    /// </summary>
    [Fact]
    public void AsyncUnaryDisposed()
    {
        static void MakeRequest(Foobar.FoobarClient client)
        {
            using var call = client.UnaryAsync(FoobarService.DefaultRequestMessage);
        }

        this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
    }

    /// <summary>
    /// Validates a successful ClientStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingSuccess()
    {
        await TestHandlerSuccess(FoobarService.MakeClientStreamingRequest, DefaultMetadataFunc());
    }

    /// <summary>
    /// Validates a failed ClientStreaming call when the service is unavailable.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingUnavailable()
    {
        await TestHandlerFailure(
            FoobarService.MakeClientStreamingRequest,
            StatusCode.Unavailable,
            validateErrorDescription: false,
            BogusServerUri);
    }

    /// <summary>
    /// Validates a failed ClientStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ClientStreamingFail()
    {
        await TestHandlerFailure(FoobarService.MakeClientStreamingRequest);
    }

    /// <summary>
    /// Validates a failed ClientStreaming call because the client is disposed before completing the RPC.
    /// </summary>
    [Fact]
    public void ClientStreamingDisposed()
    {
        static void MakeRequest(Foobar.FoobarClient client)
        {
            using var call = client.ClientStreaming();
        }

        this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
    }

    /// <summary>
    /// Validates a successful ServerStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ServerStreamingSuccess()
    {
        await TestHandlerSuccess(FoobarService.MakeServerStreamingRequest, DefaultMetadataFunc());
    }

    /// <summary>
    /// Validates a failed ServerStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ServerStreamingFail()
    {
        await TestHandlerFailure(FoobarService.MakeServerStreamingRequest);
    }

    /// <summary>
    /// Validates a failed ServerStreaming call because the client is disposed before completing the RPC.
    /// </summary>
    [Fact]
    public void ServerStreamingDisposed()
    {
        static void MakeRequest(Foobar.FoobarClient client)
        {
            using var call = client.ServerStreaming(FoobarService.DefaultRequestMessage);
        }

        this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
    }

    /// <summary>
    /// Validates a successful DuplexStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DuplexStreamingSuccess()
    {
        await TestHandlerSuccess(FoobarService.MakeDuplexStreamingRequest, DefaultMetadataFunc());
    }

    /// <summary>
    /// Validates a failed DuplexStreaming call when the service is unavailable.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DuplexStreamingUnavailable()
    {
        await TestHandlerFailure(
            FoobarService.MakeDuplexStreamingRequest,
            StatusCode.Unavailable,
            validateErrorDescription: false,
            BogusServerUri);
    }

    /// <summary>
    /// Validates a failed DuplexStreaming call.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task DuplexStreamingFail()
    {
        await TestHandlerFailure(FoobarService.MakeDuplexStreamingRequest);
    }

    /// <summary>
    /// Validates a failed DuplexStreaming call because the client is disposed before completing the RPC.
    /// </summary>
    [Fact]
    public void DuplexStreamingDisposed()
    {
        static void MakeRequest(Foobar.FoobarClient client)
        {
            using var call = client.DuplexStreaming();
        }

        this.TestActivityIsCancelledWhenHandlerDisposed(MakeRequest);
    }

    /// <summary>
    /// Validates that a downstream interceptor has access to the created Activity
    /// and that the caller always sees the correct activity, not our created Activity.
    /// </summary>
    /// <returns>A Task.</returns>
    [Fact]
    public async Task DownstreamInterceptorActivityAccess()
    {
        using var server = FoobarService.Start();
        var channel = new Channel(server.UriString, ChannelCredentials.Insecure);
        var callInvoker = channel.CreateCallInvoker();

        // Activity has a parent
        using var parentActivity = new Activity("foo");
        parentActivity.SetIdFormat(ActivityIdFormat.W3C);
        parentActivity.Start();

        // Order of interceptor invocation will be ClientTracingInterceptor -> MetadataInjector
        callInvoker = callInvoker.Intercept(
            metadata =>
            {
                // This Func is called as part of an internal MetadataInjector interceptor created by gRPC Core.
                Assert.True(Activity.Current.Source == GrpcCoreInstrumentation.ActivitySource);
                Assert.Equal(parentActivity.Id, Activity.Current.ParentId);

                // Set a tag on the Activity and make sure we can see it afterwards
                Activity.Current.SetTag("foo", "bar");
                return metadata;
            });

        var testTags = new TestActivityTags();
        var interceptorOptions = new ClientTracingInterceptorOptions { ActivityTags = testTags.Tags };
        callInvoker = callInvoker.Intercept(new ClientTracingInterceptor(interceptorOptions));
        var client = new Foobar.FoobarClient(callInvoker);

        static void ValidateNewTagOnActivity(InterceptorActivityListener listener)
        {
            var createdActivity = listener.Activity;
            Assert.Contains(createdActivity.TagObjects, t => t.Key == "foo" && (string)t.Value == "bar");
        }

        // Check the blocking async call
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            Assert.Equal(parentActivity, Activity.Current);
            var response = client.Unary(FoobarService.DefaultRequestMessage);

            Assert.Equal(parentActivity, Activity.Current);

            ValidateNewTagOnActivity(activityListener);
        }

        // Check unary async
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            Assert.Equal(parentActivity, Activity.Current);
            using var call = client.UnaryAsync(FoobarService.DefaultRequestMessage);

            Assert.Equal(parentActivity, Activity.Current);

            _ = await call.ResponseAsync;

            Assert.Equal(parentActivity, Activity.Current);

            ValidateNewTagOnActivity(activityListener);
        }

        // Check a streaming async call
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            Assert.Equal(parentActivity, Activity.Current);
            using var call = client.DuplexStreaming();

            Assert.Equal(parentActivity, Activity.Current);

            await call.RequestStream.WriteAsync(FoobarService.DefaultRequestMessage);

            Assert.Equal(parentActivity, Activity.Current);

            await call.RequestStream.CompleteAsync();

            Assert.Equal(parentActivity, Activity.Current);

            while (await call.ResponseStream.MoveNext())
            {
                Assert.Equal(parentActivity, Activity.Current);
            }

            Assert.Equal(parentActivity, Activity.Current);

            ValidateNewTagOnActivity(activityListener);
        }
    }

    /// <summary>
    /// Validates the common activity tags.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="expectedStatusCode">The expected status code.</param>
    /// <param name="recordedMessages">if set to <c>true</c> [recorded messages].</param>
    /// <param name="recordedExceptions">if set to <c>true</c> [recorded exceptions].</param>
    internal static void ValidateCommonActivityTags(
        Activity activity,
        StatusCode expectedStatusCode = StatusCode.OK,
        bool recordedMessages = false,
        bool recordedExceptions = false)
    {
        Assert.NotNull(activity);
        Assert.NotNull(activity.Tags);

        // The activity was stopped
        Assert.True(activity.Duration != default);

        // TagObjects contain non string values
        // Tags contains only string values
        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeRpcSystem && (string)t.Value == "grpc");
        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeRpcService && (string)t.Value == "OpenTelemetry.Instrumentation.GrpcCore.Tests.Foobar");
        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeRpcMethod);
        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeRpcGrpcStatusCode && (int)t.Value == (int)expectedStatusCode);

        // Cancelled is not an error.
        if (expectedStatusCode != StatusCode.OK && expectedStatusCode != StatusCode.Cancelled)
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }

        if (recordedMessages)
        {
            // all methods accept a request and return a single response
            Assert.NotNull(activity.Events);
            var requestMessage = activity.Events.FirstOrDefault(ae => ae.Name == FoobarService.DefaultRequestMessage.GetType().Name);
            var responseMessage = activity.Events.FirstOrDefault(ae => ae.Name == FoobarService.DefaultResponseMessage.GetType().Name);

            static void ValidateCommonEventAttributes(ActivityEvent activityEvent)
            {
                Assert.NotNull(activityEvent.Tags);
                Assert.Contains(activityEvent.Tags, t => t.Key == "name" && (string)t.Value == "message");
                Assert.Contains(activityEvent.Tags, t => t.Key == SemanticConventions.AttributeMessageId && (int)t.Value == 1);
            }

            Assert.NotEqual(default, requestMessage);
            Assert.NotEqual(default, responseMessage);

            ValidateCommonEventAttributes(requestMessage);
            Assert.Contains(requestMessage.Tags, t => t.Key == SemanticConventions.AttributeMessageType && (string)t.Value == "SENT");
            Assert.Contains(requestMessage.Tags, t => t.Key == SemanticConventions.AttributeMessageCompressedSize && (int)t.Value == FoobarService.DefaultRequestMessageSize);

            ValidateCommonEventAttributes(responseMessage);
            Assert.Contains(responseMessage.Tags, t => t.Key == SemanticConventions.AttributeMessageType && (string)t.Value == "RECEIVED");
            Assert.Contains(requestMessage.Tags, t => t.Key == SemanticConventions.AttributeMessageCompressedSize && (int)t.Value == FoobarService.DefaultResponseMessageSize);
        }

        if (recordedExceptions)
        {
            Assert.NotNull(activity.Events);
            Assert.Single(activity.Events, e => e.Name == SemanticConventions.AttributeExceptionEventName);

            var exceptionEvent = activity.Events.First(e => e.Name == SemanticConventions.AttributeExceptionEventName);
            Assert.NotNull(exceptionEvent.Tags);
            Assert.Contains(exceptionEvent.Tags, t => t.Key == SemanticConventions.AttributeExceptionType && (string)t.Value == typeof(RpcException).FullName);
            Assert.Contains(exceptionEvent.Tags, t => t.Key == SemanticConventions.AttributeExceptionMessage);
            Assert.Contains(exceptionEvent.Tags, t => t.Key == SemanticConventions.AttributeExceptionStacktrace);
        }
    }

    /// <summary>
    /// Tests basic handler success.
    /// </summary>
    /// <param name="clientRequestFunc">The client request function.</param>
    /// <param name="additionalMetadata">The additional metadata, if any.</param>
    /// <returns>A Task.</returns>
    private static async Task TestHandlerSuccess(Func<Foobar.FoobarClient, Metadata, Task> clientRequestFunc, Metadata additionalMetadata)
    {
        var propagator = new TestTextMapPropagator();
        PropagationContext capturedPropagationContext = default;
        Metadata capturedCarrier = null;
        var propagatorCalled = 0;
        var originalMetadataCount = additionalMetadata.Count;

        propagator.OnInject = (propagation, carrier, setter) =>
        {
            propagatorCalled++;
            capturedPropagationContext = propagation;
            capturedCarrier = (Metadata)carrier;

            // Make sure the original metadata make it through
            if (additionalMetadata != null)
            {
                Assert.Equal(capturedCarrier, additionalMetadata);
            }

            // Call the actual setter to ensure it updates the carrier.
            // It doesn't matter what we put in
            setter(capturedCarrier, "bar", "baz");
        };

        using var server = FoobarService.Start();
        var testTags = new TestActivityTags();
        var interceptorOptions = new ClientTracingInterceptorOptions
        {
            Propagator = propagator,
            RecordMessageEvents = true,
            ActivityTags = testTags.Tags,
        };

        // No Activity parent
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            var client = FoobarService.ConstructRpcClient(server.UriString, new ClientTracingInterceptor(interceptorOptions));
            await clientRequestFunc(client, additionalMetadata).ConfigureAwait(false);

            Assert.Equal(default, Activity.Current);

            var activity = activityListener.Activity;

            // Propagator was called exactly once
            Assert.Equal(1, propagatorCalled);

            // The client tracing interceptor should create a copy of the original call headers before passing to the propagator.
            // Retries that sit above this interceptor rely on the original call metadata.
            // The propagator should not have mutated the original CallOption headers.
            Assert.Equal(originalMetadataCount, additionalMetadata.Count);

            // There was no parent activity, so these will be default
            Assert.NotEqual(default, capturedPropagationContext.ActivityContext.TraceId);
            Assert.NotEqual(default, capturedPropagationContext.ActivityContext.SpanId);
            Assert.Null(activity.Parent);
            Assert.Equal(activity.TraceId, capturedPropagationContext.ActivityContext.TraceId);
            Assert.Equal(activity.SpanId, capturedPropagationContext.ActivityContext.SpanId);

            // Sanity check a valid metadata injection setter.
            Assert.NotEmpty(capturedCarrier);

            ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
            Assert.Equal(default, activity.ParentSpanId);
        }

        propagatorCalled = 0;
        capturedPropagationContext = default;

        // Activity has a parent
        using (var activityListener = new InterceptorActivityListener(testTags))
        {
            using var parentActivity = new Activity("foo");
            parentActivity.SetIdFormat(ActivityIdFormat.W3C);
            parentActivity.Start();
            var client = FoobarService.ConstructRpcClient(server.UriString, new ClientTracingInterceptor(interceptorOptions));
            await clientRequestFunc(client, additionalMetadata).ConfigureAwait(false);

            Assert.Equal(parentActivity, Activity.Current);

            // Propagator was called exactly once
            Assert.Equal(1, propagatorCalled);

            // There was a parent activity, so these will have something in them.
            Assert.NotEqual(default, capturedPropagationContext.ActivityContext.TraceId);
            Assert.NotEqual(default, capturedPropagationContext.ActivityContext.SpanId);

            var activity = activityListener.Activity;
            ValidateCommonActivityTags(activity, StatusCode.OK, interceptorOptions.RecordMessageEvents);
            Assert.Equal(parentActivity.Id, activity.ParentId);
        }
    }

    /// <summary>
    /// Tests basic handler failure. Instructs the server to fail with resources exhausted and validates the created Activity.
    /// </summary>
    /// <param name="clientRequestFunc">The client request function.</param>
    /// <param name="statusCode">The status code to use for the failure. Defaults to ResourceExhausted.</param>
    /// <param name="validateErrorDescription">if set to <c>true</c> [validate error description].</param>
    /// <param name="serverUriString">An alternate server URI string.</param>
    /// <returns>
    /// A Task.
    /// </returns>
    private static async Task TestHandlerFailure(
        Func<Foobar.FoobarClient, Metadata, Task> clientRequestFunc,
        StatusCode statusCode = StatusCode.ResourceExhausted,
        bool validateErrorDescription = true,
        string serverUriString = null)
    {
        using var server = FoobarService.Start();
        var testTags = new TestActivityTags();
        var interceptorOptions = new ClientTracingInterceptorOptions { Propagator = new TraceContextPropagator(), ActivityTags = testTags.Tags, RecordException = true };
        var client = FoobarService.ConstructRpcClient(
            serverUriString ?? server.UriString,
            new ClientTracingInterceptor(interceptorOptions),
            new List<Metadata.Entry>
            {
                new(FoobarService.RequestHeaderFailWithStatusCode, statusCode.ToString()),
                new(FoobarService.RequestHeaderErrorDescription, "fubar"),
            });

        using var activityListener = new InterceptorActivityListener(testTags);
        await Assert.ThrowsAsync<RpcException>(async () => await clientRequestFunc(client, null).ConfigureAwait(false));

        var activity = activityListener.Activity;
        ValidateCommonActivityTags(activity, statusCode, interceptorOptions.RecordMessageEvents, interceptorOptions.RecordException);

        if (validateErrorDescription)
        {
            Assert.Contains("fubar", activity.StatusDescription);
        }
    }

    /// <summary>
    /// Tests for Activity cancellation when the handler is disposed before completing the RPC.
    /// </summary>
    /// <param name="clientRequestAction">The client request action.</param>
    private void TestActivityIsCancelledWhenHandlerDisposed(Action<Foobar.FoobarClient> clientRequestAction)
    {
        using var server = FoobarService.Start();
        var testTags = new TestActivityTags();
        var clientInterceptorOptions = new ClientTracingInterceptorOptions { Propagator = new TraceContextPropagator(), ActivityTags = testTags.Tags };
        using var activityListener = new InterceptorActivityListener(testTags);
        var client = FoobarService.ConstructRpcClient(server.UriString, new ClientTracingInterceptor(clientInterceptorOptions));
        clientRequestAction(client);

        var activity = activityListener.Activity;
        ValidateCommonActivityTags(activity, StatusCode.Cancelled, false);
    }
}

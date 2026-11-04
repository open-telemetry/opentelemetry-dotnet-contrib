// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class ClientChannelInstrumentationTests
{
    [Fact]
    public void AfterRequestCompleted_DisposesSuppressionScopeWhenActivityIsNull()
    {
        var suppressionScope = new RecordingDisposable();

        ClientChannelInstrumentation.AfterRequestCompleted(
            reply: null,
            new RequestTelemetryState { SuppressionScope = suppressionScope });

        Assert.True(suppressionScope.IsDisposed);
    }

    [Fact]
    public void AfterRequestCompleted_StopsActivityWithoutRequestingData()
    {
        using var activity = new Activity("test").Start();

        ClientChannelInstrumentation.AfterRequestCompleted(
            reply: null,
            new RequestTelemetryState { Activity = activity });

        Assert.True(activity.IsStopped);
    }

    [Fact]
    public void AfterRequestCompleted_DoesNotMarkOneWayOperationWithoutReplyAsError()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        var state = new RequestTelemetryState { Activity = activity, IsOneWay = true };

        ClientChannelInstrumentation.AfterRequestCompleted(reply: null, state);

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.True(activity.IsStopped);
    }

    [Fact]
    public void AfterRequestCompleted_MarksTwoWayOperationWithoutReplyAsError()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        var state = new RequestTelemetryState { Activity = activity };

        ClientChannelInstrumentation.AfterRequestCompleted(reply: null, state);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(WcfInstrumentationConstants.ErrorTypeOther, activity.GetTagItem(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void AfterRequestCompleted_MarksExceptionAsErrorAndRecordsIt()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        var state = new RequestTelemetryState { Activity = activity };
        var exception = new InvalidOperationException("Request failed.");

        ClientChannelInstrumentation.AfterRequestCompleted(reply: null, state, exception);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem(SemanticConventions.AttributeErrorType));
        Assert.Single(activity.Events);
    }

    [Fact]
    public void AfterRequestCompleted_UsesFaultCodeForFaultException()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        var state = new RequestTelemetryState { Activity = activity };
        var exception = new FaultException(new FaultReason("Request failed."), new FaultCode("TestFault"), "urn:fault");

        ClientChannelInstrumentation.AfterRequestCompleted(reply: null, state, exception);

        Assert.Equal("TestFault", activity.GetTagItem(SemanticConventions.AttributeRpcResponseStatusCode));
        Assert.Equal("TestFault", activity.GetTagItem(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void AfterRequestCompleted_RespectsDisabledRpcAttributesAndExceptionRecording()
    {
        using var activityScope = new ActivityScope(emitNewRpcAttributes: false, recordException: false);
        var activity = activityScope.Activity;
        var state = new RequestTelemetryState { Activity = activity };
        var exception = new FaultException(new FaultReason("Request failed."), new FaultCode("TestFault"), "urn:fault");

        ClientChannelInstrumentation.AfterRequestCompleted(reply: null, state, exception);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.GetTagItem(SemanticConventions.AttributeRpcResponseStatusCode));
        Assert.Null(activity.GetTagItem(SemanticConventions.AttributeErrorType));
        Assert.Empty(activity.Events);
    }

    [Fact]
    public void AfterRequestCompleted_DoesNotMarkSuccessfulReplyAsError()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        using var reply = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        ClientChannelInstrumentation.AfterRequestCompleted(reply, new RequestTelemetryState { Activity = activity });

        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("urn:reply", activity.GetTagItem(WcfInstrumentationConstants.AttributeSoapReplyAction));
    }

    [Fact]
    public void AfterRequestCompleted_EnrichesReplyWhenConfigured()
    {
        using var activityScope = new ActivityScope(enrich: static (activity, eventName, _) => activity.SetTag("enriched.event", eventName));
        var activity = activityScope.Activity;
        using var reply = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        ClientChannelInstrumentation.AfterRequestCompleted(reply, new RequestTelemetryState { Activity = activity });

        Assert.Equal(WcfEnrichEventNames.AfterReceiveReply, activity.GetTagItem("enriched.event"));
    }

    [Fact]
    public void AfterRequestCompleted_MarksFaultReplyAsError()
    {
        using var activityScope = new ActivityScope();
        var activity = activityScope.Activity;
        using var reply = Message.CreateMessage(
            MessageVersion.Soap11,
            new FaultCode("TestFault"),
            "Request failed.",
            "urn:fault");

        ClientChannelInstrumentation.AfterRequestCompleted(reply, new RequestTelemetryState { Activity = activity });

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(WcfInstrumentationConstants.ErrorTypeOther, activity.GetTagItem(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void BeforeSendRequestSetsNetworkPeerTagsForIpLiteralRemoteAddress()
    {
        var stoppedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);

        WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { EmitNewRpcAttributes = true };

        try
        {
            using var message = Message.CreateMessage(MessageVersion.Default, "http://opentelemetry.io/Service/Execute");

            var state = ClientChannelInstrumentation.BeforeSendRequest(message, new Uri("http://127.0.0.1:8080/Service"));
            state.Activity?.Stop();
            state.SuppressionScope?.Dispose();

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal("127.0.0.1", activity.GetTagItem(SemanticConventions.AttributeNetworkPeerAddress));
            Assert.Equal(8080, activity.GetTagItem(SemanticConventions.AttributeNetworkPeerPort));
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void BeforeSendRequestDoesNotLeakHeadersIntoSharedHttpRequestMessageProperty()
    {
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(activityListener);

        WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { SuppressDownstreamInstrumentation = true };

        var originalPropagator = Propagators.DefaultTextMapPropagator;
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));

        try
        {
            // An application-owned property attached to every call.
            var shared = new HttpRequestMessageProperty { Method = "PUT", QueryString = "a=b", SuppressEntityBody = true };
            shared.Headers["X-Api-Key"] = "secret";

            // First call carries baggage.
            using (var first = Message.CreateMessage(MessageVersion.Soap11, "urn:first"))
            {
                first.Properties[HttpRequestMessageProperty.Name] = shared;

                var previous = Baggage.Current;
                Baggage.Current = Baggage.SetBaggage("tenant", "mallory");
                RequestTelemetryState state;
                try
                {
                    state = ClientChannelInstrumentation.BeforeSendRequest(first, null);
                }
                finally
                {
                    Baggage.Current = previous;
                }

                state.Activity?.Stop();
                state.SuppressionScope?.Dispose();

                var sent = Assert.IsType<HttpRequestMessageProperty>(first.Properties[HttpRequestMessageProperty.Name]);
                Assert.NotSame(shared, sent);
                Assert.Equal("tenant=mallory", sent.Headers["baggage"]);
                Assert.NotNull(sent.Headers["traceparent"]);
                Assert.Equal("secret", sent.Headers["X-Api-Key"]);
                Assert.Equal("PUT", sent.Method);
                Assert.Equal("a=b", sent.QueryString);
                Assert.True(sent.SuppressEntityBody);
            }

            // The application's own property must be untouched.
            Assert.Equal(["X-Api-Key"], shared.Headers.AllKeys);

            // A second call without baggage must not see anything from the first.
            using (var second = Message.CreateMessage(MessageVersion.Soap11, "urn:second"))
            {
                second.Properties[HttpRequestMessageProperty.Name] = shared;

                var state = ClientChannelInstrumentation.BeforeSendRequest(second, null);
                state.Activity?.Stop();
                state.SuppressionScope?.Dispose();

                var sent = Assert.IsType<HttpRequestMessageProperty>(second.Properties[HttpRequestMessageProperty.Name]);
                Assert.NotSame(shared, sent);
                Assert.Null(sent.Headers["baggage"]);
                Assert.Null(sent.Headers["tracestate"]);
                Assert.Equal("secret", sent.Headers["X-Api-Key"]);
            }

            Assert.Equal(["X-Api-Key"], shared.Headers.AllKeys);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(originalPropagator);
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void CreateCopyPreservesRawHeaderValues()
    {
        const string digest = "Digest username=\"a\\\", b\", realm=\"r\", nonce=\"n\"";

        var original = new HttpRequestMessageProperty();
        original.Headers["Authorization"] = digest;
        original.Headers.Add("Accept", "text/xml");
        original.Headers.Add("Accept", "application/json");
        original.Headers["X-Quoted"] = "\"a, b\", c";

        var copy = Assert.IsType<HttpRequestMessageProperty>(HttpRequestMessagePropertyWrapper.CreateCopy(original));

        Assert.NotSame(original, copy);
        Assert.NotSame(original.Headers, copy.Headers);
        Assert.Equal(original.Headers.AllKeys, copy.Headers.AllKeys);
        foreach (var key in original.Headers.AllKeys)
        {
            Assert.Equal(original.Headers[key], copy.Headers[key]);
        }

        Assert.Equal(digest, copy.Headers["Authorization"]);
    }

    private sealed class ActivityScope : IDisposable
    {
        private readonly ActivityListener listener;

        public ActivityScope(
            bool emitNewRpcAttributes = true,
            bool recordException = true,
            Action<Activity, string, object>? enrich = null)
        {
            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions
            {
                EmitNewRpcAttributes = emitNewRpcAttributes,
                RecordException = recordException,
                Enrich = enrich,
            };

            this.listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(this.listener);

            this.Activity = WcfInstrumentationActivitySource.Get(WcfInstrumentationActivitySource.Options)
                .StartActivity("test", ActivityKind.Client)!;
        }

        public Activity Activity { get; }

        public void Dispose()
        {
            if (!this.Activity.IsStopped)
            {
                this.Activity.Stop();
            }

            this.listener.Dispose();
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    private sealed class RecordingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
            => this.IsDisposed = true;
    }
}

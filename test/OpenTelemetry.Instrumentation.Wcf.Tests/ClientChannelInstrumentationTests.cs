// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
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
        var exception = new FaultException("Request failed.", new FaultCode("TestFault"));

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
        var exception = new FaultException("Request failed.", new FaultCode("TestFault"));

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
            MessageFault.CreateFault(new FaultCode("TestFault"), "Request failed."),
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

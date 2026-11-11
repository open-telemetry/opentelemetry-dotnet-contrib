// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class InstrumentedChannelAsyncCallbackTests
{
    [Fact]
    public void BeginRequest_CleansUpTelemetryWhenInnerBeginRequestThrows()
    {
        var stoppedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);

        WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions();

        try
        {
            var inner = new RecordingRequestChannel
            {
                BeginRequestException = new InvalidOperationException("BeginRequest failed."),
            };
            var channel = (IRequestChannel)new InstrumentedRequestChannel(inner);

            using var message = Message.CreateMessage(MessageVersion.Soap11, "urn:test");
            var exception = Assert.Throws<InvalidOperationException>(() => channel.BeginRequest(message, callback: null, state: null));

            Assert.Same(inner.BeginRequestException, exception);
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void BeginRequest_AllowsNullAsyncCallback()
    {
        var inner = new RecordingRequestChannel();
        var channel = (IRequestChannel)new InstrumentedRequestChannel(inner);
        var state = new object();

        var asyncResult = channel.BeginRequest(
            Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
            callback: null,
            state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginRequestArgs);
        Assert.Null(inner.LastBeginRequestArgs[1]);
        Assert.Same(state, inner.LastBeginRequestArgs[2]);
    }

    [Fact]
    public void BeginRequest_AllowsSuppressedExecutionContextFlow()
    {
        var inner = new RecordingRequestChannel();
        var channel = (IRequestChannel)new InstrumentedRequestChannel(inner);
        var state = new object();

        using (ExecutionContext.SuppressFlow())
        {
            var asyncResult = channel.BeginRequest(
                Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
                callback: null,
                state);

            Assert.NotNull(asyncResult);
        }

        Assert.NotNull(inner.LastBeginRequestArgs);
        Assert.Null(inner.LastBeginRequestArgs[1]);
        Assert.Same(state, inner.LastBeginRequestArgs[2]);
    }

    [Fact]
    public void BeginSend_AllowsNullAsyncCallback()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
        var state = new object();

        var asyncResult = channel.BeginSend(
            Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
            callback: null,
            state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginSendArgs);
        Assert.Null(inner.LastBeginSendArgs[1]);
        Assert.Same(state, inner.LastBeginSendArgs[2]);
    }

    [Fact]
    public void BeginSend_AllowsSuppressedExecutionContextFlow()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
        var state = new object();

        using (ExecutionContext.SuppressFlow())
        {
            var asyncResult = channel.BeginSend(
                Message.CreateMessage(MessageVersion.Soap11, "urn:test"),
                callback: null,
                state);

            Assert.NotNull(asyncResult);
        }

        Assert.NotNull(inner.LastBeginSendArgs);
        Assert.Null(inner.LastBeginSendArgs[1]);
        Assert.Same(state, inner.LastBeginSendArgs[2]);
    }

    [Fact]
    public void BeginSend_OneWayStopsOnEndSend()
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel();
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
            var message = Message.CreateMessage(MessageVersion.Soap11, "urn:test");
            message.Properties.Add(
                TelemetryContextMessageProperty.Name,
                new TelemetryContextMessageProperty(new Dictionary<string, ActionMetadata>
                {
                    ["urn:test"] = new ActionMetadata(contractName: null, operationName: "Test", isOneWay: true),
                }));

            var result = channel.BeginSend(message, callback: null, state: null);

            Assert.Empty(stoppedActivities);

            channel.EndSend(result);

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void EndSend_OneWayFailureMarksActivityAsError()
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel
            {
                EndSendException = new InvalidOperationException("EndSend failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
            var message = Message.CreateMessage(MessageVersion.Soap11, "urn:test");
            message.Properties.Add(
                TelemetryContextMessageProperty.Name,
                new TelemetryContextMessageProperty(new Dictionary<string, ActionMetadata>
                {
                    ["urn:test"] = new ActionMetadata(contractName: null, operationName: "Test", isOneWay: true),
                }));

            var result = channel.BeginSend(message, callback: null, state: null);

            Assert.Throws<InvalidOperationException>(() => channel.EndSend(result));

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void EndSend_TwoWayFailureDoesNotCompleteTelemetryTwiceForLateReply()
    {
        var enrichEvents = new List<string>();
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions
            {
                Enrich = (_, eventName, _) => enrichEvents.Add(eventName),
            };

            var inner = new RecordingDuplexChannel
            {
                EndSendException = new TimeoutException("EndSend failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();
            var result = channel.BeginSend(request, callback: null, state: null);

            Assert.Throws<TimeoutException>(() => channel.EndSend(result));
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);

            // The server processed the request anyway and its reply arrives after EndSend failed.
            inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
            using var reply = channel.Receive();

            Assert.Same(inner.ReceiveMessage, reply);
            Assert.DoesNotContain(WcfEnrichEventNames.AfterReceiveReply, enrichEvents);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void EndSend_DoesNotCompleteTelemetryTwiceWhenReplyArrivesBeforeSendFailure()
    {
        var enrichEvents = new List<string>();
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions
            {
                Enrich = (_, eventName, _) => enrichEvents.Add(eventName),
            };

            var inner = new RecordingDuplexChannel
            {
                EndSendException = new TimeoutException("EndSend failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();
            var result = channel.BeginSend(request, callback: null, state: null);

            // The reply completes the request before the send failure is reported to the caller.
            inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
            using var reply = channel.Receive();

            Assert.Single(stoppedActivities);

            Assert.Throws<TimeoutException>(() => channel.EndSend(result));

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
            Assert.Equal(
                [WcfEnrichEventNames.BeforeSendRequest, WcfEnrichEventNames.AfterReceiveReply],
                enrichEvents);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_TwoWayFailureWithoutMessageIdMarksActivityAsError()
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel
            {
                SendException = new TimeoutException("Send failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            // Without a MessageId no reply can be correlated, so the request is not tracked.
            using var message = Message.CreateMessage(MessageVersion.Soap11, "urn:test");
            Assert.Null(message.Headers.MessageId);

            Assert.Throws<TimeoutException>(() => channel.Send(message));

            Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_TwoWayFailureDoesNotCompleteTelemetryTwiceForLateReply()
    {
        var enrichEvents = new List<string>();
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions
            {
                Enrich = (_, eventName, _) => enrichEvents.Add(eventName),
            };

            var inner = new RecordingDuplexChannel
            {
                SendException = new TimeoutException("Send failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();

            Assert.Throws<TimeoutException>(() => channel.Send(request));
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);

            // The server processed the request anyway and its reply arrives after Send failed.
            inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
            using var reply = channel.Receive();

            Assert.Same(inner.ReceiveMessage, reply);
            Assert.DoesNotContain(WcfEnrichEventNames.AfterReceiveReply, enrichEvents);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_TwoWayFailureRemovesTrackedTelemetryState()
    {
        try
        {
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(new List<Activity>())
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel
            {
                SendException = new TimeoutException("Send failed."),
            };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();

            Assert.Throws<TimeoutException>(() => channel.Send(request));

            // The state must not be kept until the telemetry timeout fires.
            Assert.Null(RequestTelemetryStateTracker.PopTelemetryState(CreateReply(request.Headers.MessageId!)));
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public async Task Send_TwoWayWithInfiniteTimeoutStaysTrackedWhenTelemetryTimerFires()
    {
        try
        {
            // The telemetry timer exports the first activity on another thread while this one polls for it.
            var stoppedActivities = new SynchronizedCollection<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel();
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.MaxValue);

            // WCF uses TimeSpan.MaxValue to disable a timeout; it is far beyond what Timer.Change accepts.
            using var request = CreateTwoWayRequest();
            channel.Send(request);

            // Makes the telemetry timer fire while the infinite-timeout request is still outstanding.
            using var timingOutRequest = CreateTwoWayRequest();
            channel.Send(timingOutRequest, TimeSpan.FromMilliseconds(100));

            for (var i = 0; i < 50 && stoppedActivities.Count == 0; i++)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            var stoppedActivity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stoppedActivity.Status);

            inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
            using var reply = channel.Receive();

            Assert.Equal(2, stoppedActivities.Count);
            Assert.Equal(ActivityStatusCode.Unset, stoppedActivities[1].Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Send_TwoWayCompletesAndReleasesOutstandingRequestsWhenChannelClosesOrFaults(bool fault)
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            var inner = new RecordingDuplexChannel();
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.MaxValue);
            var otherInner = new RecordingDuplexChannel();
            var otherChannel = new InstrumentedDuplexChannel(otherInner, TimeSpan.MaxValue);

            using var request = CreateTwoWayRequest();
            channel.Send(request);

            using var otherRequest = CreateTwoWayRequest();
            otherChannel.Send(otherRequest);

            Assert.Empty(stoppedActivities);

            if (fault)
            {
                inner.RaiseFaulted();
            }
            else
            {
                inner.RaiseClosed();
            }

            // Only the requests of the channel that went away are completed, as errors.
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);
            Assert.Null(RequestTelemetryStateTracker.PopTelemetryState(CreateReply(request.Headers.MessageId!)));

            // The other channel's request is still tracked and completes when its reply arrives.
            otherInner.ReceiveMessage = CreateReply(otherRequest.Headers.MessageId!);
            using var reply = otherChannel.Receive();

            Assert.Equal(2, stoppedActivities.Count);
            Assert.Equal(ActivityStatusCode.Unset, stoppedActivities[1].Status);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Send_TwoWayDoesNotChangeAmbientContextWhenCompletingWithoutReply(
        bool suppressFlow,
        bool completeOnEndSend)
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { SuppressDownstreamInstrumentation = true };

            var callerState = new object();
            var ambientState = new AsyncLocal<object?> { Value = callerState };
            bool? callbackFlowSuppressed = null;
            Task<object?>? callbackTask = null;
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == WcfInstrumentationActivitySource.ActivitySource.Name,
                ActivityStopped = _ =>
                {
                    callbackFlowSuppressed = ExecutionContext.IsFlowSuppressed();
                    callbackTask = Task.Run<object?>(() => ambientState.Value);
                },
            };
            ActivitySource.AddActivityListener(activityListener);

            var inner = new RecordingDuplexChannel();
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();
            IAsyncResult? result = null;
            if (completeOnEndSend)
            {
                result = channel.BeginSend(request, callback: null, state: null);
                inner.RaiseClosed();
                Assert.Empty(stoppedActivities);
            }
            else
            {
                channel.Send(request);
            }

            using var current = new Activity("Current").Start();
            using var suppression = SuppressInstrumentationScope.Begin();
            Assert.Same(current, Activity.Current);

            AsyncFlowControl? flowControl = suppressFlow ? ExecutionContext.SuppressFlow() : null;
            try
            {
                if (completeOnEndSend)
                {
                    channel.EndSend(result!);
                }
                else
                {
                    inner.RaiseClosed();
                }

                Assert.Single(stoppedActivities);

                // Completing the request must not replace the current activity or end the caller's suppression.
                Assert.Same(current, Activity.Current);
                Assert.Same(callerState, ambientState.Value);
                Assert.True(Sdk.SuppressInstrumentation);
                Assert.Equal(suppressFlow, ExecutionContext.IsFlowSuppressed());
            }
            finally
            {
                flowControl?.Undo();
            }

            Assert.NotNull(callbackTask);
            var completed = await Task.WhenAny(
                callbackTask,
                Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.Same(callbackTask, completed);
            Assert.Same(suppressFlow ? null : callerState, await callbackTask);
            Assert.Equal(suppressFlow, callbackFlowSuppressed);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_TwoWayDoesNotCompleteTelemetryAgainWhenChannelClosesAfterReply()
    {
        var stoppedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

        using var request = CreateTwoWayRequest();
        channel.Send(request);

        inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
        using var reply = channel.Receive();

        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(stoppedActivities).Status);

        inner.RaiseClosed();

        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(stoppedActivities).Status);
    }

    [Fact]
    public void Send_TwoWayRecordsExceptionWhenChannelFaultsBeforeSendThrows()
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { RecordException = true };

            var inner = new RecordingDuplexChannel { SendException = new TimeoutException("Send failed.") };
            inner.OnSend = inner.RaiseFaulted;
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();

            Assert.Throws<TimeoutException>(() => channel.Send(request));

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Contains(activity.Events, e => e.Name == "exception");
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void EndSend_TwoWayRecordsExceptionWhenChannelFaultsBeforeEndSendThrows()
    {
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions { RecordException = true };

            var inner = new RecordingDuplexChannel { EndSendException = new TimeoutException("EndSend failed.") };
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();
            var result = channel.BeginSend(request, callback: null, state: null);

            inner.RaiseFaulted();

            // The request is left for EndSend to complete, as it knows why the send failed.
            Assert.Empty(stoppedActivities);

            Assert.Throws<TimeoutException>(() => channel.EndSend(result));

            var activity = Assert.Single(stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Contains(activity.Events, e => e.Name == "exception");
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_TwoWayCompletesTelemetryWhenChannelFaultsDuringSendThatSucceeds()
    {
        var stoppedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var inner = new RecordingDuplexChannel();
        inner.OnSend = inner.RaiseFaulted;
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.MaxValue);

        using var request = CreateTwoWayRequest();
        channel.Send(request);

        // No reply can arrive on a faulted channel, so the request must not be left outstanding.
        Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);
        Assert.Null(RequestTelemetryStateTracker.PopTelemetryState(CreateReply(request.Headers.MessageId!)));
    }

    [Fact]
    public void EndSend_TwoWayCompletesTelemetryWhenChannelFaultsBeforeEndSendThatSucceeds()
    {
        var stoppedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.MaxValue);

        using var request = CreateTwoWayRequest();
        var result = channel.BeginSend(request, callback: null, state: null);

        inner.RaiseFaulted();

        Assert.Empty(stoppedActivities);

        channel.EndSend(result);

        Assert.Equal(ActivityStatusCode.Error, Assert.Single(stoppedActivities).Status);
        Assert.Null(RequestTelemetryStateTracker.PopTelemetryState(CreateReply(request.Headers.MessageId!)));
    }

    [Fact]
    public async Task TimeoutCallbackExceptionsDoNotEscapeTheTimerThread()
    {
        var invoked = 0;
        var allInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnTimedOut(RequestTelemetryState state)
        {
            if (Interlocked.Increment(ref invoked) == 2)
            {
                allInvoked.TrySetResult(true);
            }

            throw new InvalidOperationException("Callback failed.");
        }

        using var request1 = CreateTwoWayRequest();
        using var request2 = CreateTwoWayRequest();

        RequestTelemetryStateTracker.PushTelemetryState(request1, new RequestTelemetryState(), TimeSpan.FromMilliseconds(50), OnTimedOut);
        RequestTelemetryStateTracker.PushTelemetryState(request2, new RequestTelemetryState(), TimeSpan.FromMilliseconds(50), OnTimedOut);

        var completed = await Task.WhenAny(allInvoked.Task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(allInvoked.Task, completed);
    }

    [Fact]
    public void Send_TwoWaySuccessCompletesTelemetryWhenReplyArrives()
    {
        var enrichEvents = new List<string>();
        try
        {
            var stoppedActivities = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(stoppedActivities)
                .AddWcfInstrumentation()
                .Build();

            WcfInstrumentationActivitySource.Options = new WcfInstrumentationOptions
            {
                Enrich = (_, eventName, _) => enrichEvents.Add(eventName),
            };

            var inner = new RecordingDuplexChannel();
            var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(30));

            using var request = CreateTwoWayRequest();

            channel.Send(request);

            Assert.Empty(stoppedActivities);

            inner.ReceiveMessage = CreateReply(request.Headers.MessageId!);
            using var reply = channel.Receive();

            Assert.Single(stoppedActivities);
            Assert.Equal(
                [WcfEnrichEventNames.BeforeSendRequest, WcfEnrichEventNames.AfterReceiveReply],
                enrichEvents);
        }
        finally
        {
            WcfInstrumentationActivitySource.Options = null;
        }
    }

    [Fact]
    public void Send_AllowsSuppressedExecutionContextFlow()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));

        using (ExecutionContext.SuppressFlow())
        {
            channel.Send(Message.CreateMessage(MessageVersion.Soap11, "urn:test"));
        }

        Assert.True(inner.SendCalled);
    }

    [Fact]
    public void Send_AllowsNullInnerRemoteAddress()
    {
        var inner = new RecordingDuplexChannel { ReturnNullRemoteAddress = true };
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));

        channel.Send(Message.CreateMessage(MessageVersion.Soap11, "urn:test"));

        Assert.True(inner.SendCalled);
    }

    [Fact]
    public void BeginReceive_AllowsNullAsyncCallback()
    {
        var inner = new RecordingDuplexChannel();
        var channel = new InstrumentedDuplexChannel(inner, TimeSpan.FromSeconds(1));
        var state = new object();

        var asyncResult = channel.BeginReceive(callback: null, state);

        Assert.NotNull(asyncResult);
        Assert.NotNull(inner.LastBeginReceiveArgs);
        Assert.Null(inner.LastBeginReceiveArgs[0]);
        Assert.Same(state, inner.LastBeginReceiveArgs[1]);
    }

    // WCF's RequestReplyCorrelator assigns a MessageId to outgoing request messages before they
    // reach the channel, which is what allows the reply to be correlated with the request.
    private static Message CreateTwoWayRequest()
    {
        var message = Message.CreateMessage(MessageVersion.Soap11WSAddressing10, "urn:test");
        message.Headers.MessageId = new UniqueId(Guid.NewGuid());
        message.Properties.Add(
            TelemetryContextMessageProperty.Name,
            new TelemetryContextMessageProperty(new Dictionary<string, ActionMetadata>
            {
                ["urn:test"] = new ActionMetadata(contractName: null, operationName: "Test", isOneWay: false),
            }));
        return message;
    }

    private static Message CreateReply(UniqueId relatesTo)
    {
        var reply = Message.CreateMessage(MessageVersion.Soap11WSAddressing10, "urn:reply");
        reply.Headers.RelatesTo = relatesTo;
        return reply;
    }

    private sealed class FakeAsyncResult : IAsyncResult
    {
        public FakeAsyncResult(object? asyncState)
        {
            this.AsyncState = asyncState;
        }

        public object? AsyncState { get; }

        public WaitHandle AsyncWaitHandle { get; } = new ManualResetEvent(initialState: true);

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }

    private abstract class RecordingChannel : IChannel
    {
        private EventHandler? closed;
        private EventHandler? faulted;

        event EventHandler ICommunicationObject.Closed
        {
            add => this.closed += value;
            remove => this.closed -= value;
        }

        event EventHandler ICommunicationObject.Closing
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Faulted
        {
            add => this.faulted += value;
            remove => this.faulted -= value;
        }

        event EventHandler ICommunicationObject.Opened
        {
            add
            {
            }

            remove
            {
            }
        }

        event EventHandler ICommunicationObject.Opening
        {
            add
            {
            }

            remove
            {
            }
        }

        public CommunicationState State => CommunicationState.Opened;

        public virtual T? GetProperty<T>()
            where T : class
            => default;

        public void RaiseClosed() => this.closed?.Invoke(this, EventArgs.Empty);

        public void RaiseFaulted() => this.faulted?.Invoke(this, EventArgs.Empty);

        public void Abort()
        {
        }

        public IAsyncResult BeginClose(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public void Close()
        {
        }

        public void Close(TimeSpan timeout)
        {
        }

        public void EndClose(IAsyncResult result)
        {
        }

        public void EndOpen(IAsyncResult result)
        {
        }

        public void Open()
        {
        }

        public void Open(TimeSpan timeout)
        {
        }
    }

    private sealed class RecordingRequestChannel : RecordingChannel, IRequestChannel
    {
        public Exception? BeginRequestException { get; init; }

        public object?[]? LastBeginRequestArgs { get; private set; }

        public EndpointAddress RemoteAddress { get; } = new("net.tcp://localhost/Service");

        public Uri Via { get; } = new("net.tcp://localhost/Service");

        public Message Request(Message message)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Request(Message message, TimeSpan timeout)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
        {
            if (this.BeginRequestException != null)
            {
                throw this.BeginRequestException;
            }

            this.LastBeginRequestArgs = [message, callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            if (this.BeginRequestException != null)
            {
                throw this.BeginRequestException;
            }

            this.LastBeginRequestArgs = [message, timeout, callback, state];
            return new FakeAsyncResult(state);
        }

        public Message EndRequest(IAsyncResult result)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
    }

    private sealed class RecordingDuplexChannel : RecordingChannel, IDuplexChannel
    {
        public object?[]? LastBeginReceiveArgs { get; private set; }

        public object?[]? LastBeginSendArgs { get; private set; }

        public Exception? EndSendException { get; init; }

        public Exception? SendException { get; init; }

        public Action? OnSend { get; set; }

        public Message? ReceiveMessage { get; set; }

        public bool ReturnNullRemoteAddress { get; init; }

        public bool SendCalled { get; private set; }

        public EndpointAddress LocalAddress { get; } = new("net.tcp://localhost/Local");

        public EndpointAddress RemoteAddress => this.ReturnNullRemoteAddress ? null! : new("net.tcp://localhost/Service");

        public Uri Via { get; } = new("net.tcp://localhost/Service");

        public void Send(Message message)
        {
            this.SendCalled = true;
            this.OnSend?.Invoke();
            if (this.SendException != null)
            {
                throw this.SendException;
            }
        }

        public void Send(Message message, TimeSpan timeout)
        {
            this.SendCalled = true;
            this.OnSend?.Invoke();
            if (this.SendException != null)
            {
                throw this.SendException;
            }
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            this.LastBeginSendArgs = [message, callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            this.LastBeginSendArgs = [message, timeout, callback, state];
            return new FakeAsyncResult(state);
        }

        public void EndSend(IAsyncResult result)
        {
            if (this.EndSendException != null)
            {
                throw this.EndSendException;
            }
        }

        public Message Receive()
            => this.ReceiveMessage ?? Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public Message Receive(TimeSpan timeout)
            => this.ReceiveMessage ?? Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            this.LastBeginReceiveArgs = [callback, state];
            return new FakeAsyncResult(state);
        }

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            this.LastBeginReceiveArgs = [timeout, callback, state];
            return new FakeAsyncResult(state);
        }

        public Message EndReceive(IAsyncResult result)
            => Message.CreateMessage(MessageVersion.Soap11, "urn:reply");

        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            message = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
            return true;
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            message = Message.CreateMessage(MessageVersion.Soap11, "urn:reply");
            return true;
        }

        public bool WaitForMessage(TimeSpan timeout)
            => true;

        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
            => new FakeAsyncResult(state);

        public bool EndWaitForMessage(IAsyncResult result)
            => true;
    }
}

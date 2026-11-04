// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel;
using System.ServiceModel.Channels;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class InstrumentedDuplexChannel : InstrumentedChannel<IDuplexChannel>, IDuplexChannel
{
    private readonly TimeSpan telemetryTimeout;

    public InstrumentedDuplexChannel(IDuplexChannel inner, TimeSpan telemetryTimeout)
        : base(inner)
    {
        this.telemetryTimeout = telemetryTimeout;

        // Replies are only delivered through this channel, so once it is closed, aborted or faulted
        // the requests still waiting for one will never complete and must not be tracked any longer.
        inner.Closed += this.OnChannelClosedOrFaulted;
        inner.Faulted += this.OnChannelClosedOrFaulted;
    }

    public EndpointAddress LocalAddress => this.Inner.LocalAddress;

    public EndpointAddress RemoteAddress => this.Inner.RemoteAddress;

    public Uri Via => this.Inner.Via;

    public void Send(Message message)
    {
        Guard.ThrowIfNull(message);

        this.SendInternal(message, this.telemetryTimeout, _ => this.Inner.Send(message));
    }

    public void Send(Message message, TimeSpan timeout)
    {
        Guard.ThrowIfNull(message);

        this.SendInternal(message, timeout, _ => this.Inner.Send(message, timeout));
    }

    public IAsyncResult BeginSend(Message message, AsyncCallback? callback, object? state)
    {
        Guard.ThrowIfNull(message);

        return this.SendInternal(message, this.telemetryTimeout, (cb, s) => this.Inner.BeginSend(message, cb, s), callback, state);
    }

    public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback? callback, object? state)
    {
        Guard.ThrowIfNull(message);

        return this.SendInternal(message, timeout, (cb, s) => this.Inner.BeginSend(message, timeout, cb, s), callback, state);
    }

    public void EndSend(IAsyncResult result)
    {
        Guard.ThrowIfNull(result);

        var asyncResult = (AsyncResultWithTelemetryState)result;
        try
        {
            this.Inner.EndSend(asyncResult.Inner);
            if (asyncResult.TelemetryState.IsOneWay)
            {
                ClientChannelInstrumentation.AfterRequestCompleted(null, asyncResult.TelemetryState);
            }
            else
            {
                OnSendCompleted(asyncResult.TelemetryState);
            }
        }
        catch (Exception ex)
        {
            RequestTelemetryStateTracker.RemoveTelemetryState(asyncResult.TelemetryState);
            ClientChannelInstrumentation.AfterRequestCompleted(null, asyncResult.TelemetryState, ex);
            throw;
        }
    }

    public Message Receive()
    {
        var message = this.Inner.Receive();
        HandleReceivedMessage(message);
        return message;
    }

    public Message Receive(TimeSpan timeout)
    {
        var message = this.Inner.Receive(timeout);
        HandleReceivedMessage(message);
        return message;
    }

    public IAsyncResult BeginReceive(AsyncCallback? callback, object? state)
    {
        return this.Inner.BeginReceive(callback, state);
    }

    public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback? callback, object? state)
    {
        return this.Inner.BeginReceive(timeout, callback, state);
    }

    public Message EndReceive(IAsyncResult result)
    {
        Guard.ThrowIfNull(result);

        var message = this.Inner.EndReceive(result);
        HandleReceivedMessage(message);
        return message;
    }

    public bool TryReceive(TimeSpan timeout, out Message message)
    {
        var returnValue = this.Inner.TryReceive(timeout, out message);
        HandleReceivedMessage(message);
        return returnValue;
    }

    public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback? callback, object? state)
    {
        return this.Inner.BeginTryReceive(timeout, callback, state);
    }

    public bool EndTryReceive(IAsyncResult result, out Message message)
    {
        Guard.ThrowIfNull(result);

        var returnValue = this.Inner.EndTryReceive(result, out message);
        HandleReceivedMessage(message);
        return returnValue;
    }

    public bool WaitForMessage(TimeSpan timeout)
    {
        return this.Inner.WaitForMessage(timeout);
    }

    public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback? callback, object? state)
    {
        return this.Inner.BeginWaitForMessage(timeout, callback, state);
    }

    public bool EndWaitForMessage(IAsyncResult result)
    {
        Guard.ThrowIfNull(result);

        return this.Inner.EndWaitForMessage(result);
    }

    private static void HandleReceivedMessage(Message message)
    {
        var telemetryState = RequestTelemetryStateTracker.PopTelemetryState(message);
        if (telemetryState != null)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(message, telemetryState);
        }
    }

    private static void OnTelemetryStateTimedOut(RequestTelemetryState telemetryState)
    {
        ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState);
    }

    private static void RunInIsolatedContext(Action action)
    {
        // Completing a request stops its activity and ends its instrumentation suppression scope, which
        // change the ambient context. That must not leak into the thread that closed or faulted the
        // channel, or that called EndSend, as its own Activity.Current would be replaced.
        //
        // Capture() returns null while the flow of the execution context is suppressed, so the flow is
        // restored to capture a child context. Reapply suppression inside that context so callbacks cannot
        // flow the caller's AsyncLocal values into queued work, then restore the caller's suppression afterwards.
        var flowWasSuppressed = ExecutionContext.IsFlowSuppressed();
        if (flowWasSuppressed)
        {
            ExecutionContext.RestoreFlow();
        }

        try
        {
            var executionContext = ExecutionContext.Capture();
            if (executionContext == null)
            {
                action();
            }
            else if (flowWasSuppressed)
            {
                ExecutionContext.Run(
                    executionContext,
                    static state =>
                    {
                        using var flowControl = ExecutionContext.SuppressFlow();
                        ((Action)state!)();
                    },
                    action);
            }
            else
            {
                ExecutionContext.Run(executionContext, static state => ((Action)state!)(), action);
            }
        }
        finally
        {
            if (flowWasSuppressed)
            {
                _ = ExecutionContext.SuppressFlow();
            }
        }
    }

    private static void CompleteWithoutReply(RequestTelemetryState telemetryState)
    {
        try
        {
            OnTelemetryStateTimedOut(telemetryState);
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.TelemetryTimeoutException(ex);
        }
    }

    private static void OnSendCompleted(RequestTelemetryState telemetryState)
    {
        // The channel went away while sending, so no reply will arrive for this request.
        if (telemetryState.MarkSendCompleted())
        {
            RunInIsolatedContext(() => CompleteWithoutReply(telemetryState));
        }
    }

    private void OnChannelClosedOrFaulted(object? sender, EventArgs e)
    {
        var telemetryStates = RequestTelemetryStateTracker.RemoveTelemetryStates(this);
        if (telemetryStates.Count == 0)
        {
            return;
        }

        RunInIsolatedContext(() =>
        {
            foreach (var telemetryState in telemetryStates)
            {
                // A channel typically faults before the failed send throws. Leave those requests to the
                // send's failure path, which knows the exception, and complete them here only if it succeeds.
                if (!telemetryState.TryDeferCompletionWhileSending())
                {
                    CompleteWithoutReply(telemetryState);
                }
            }
        });
    }

    private IAsyncResult SendInternal(Message message, TimeSpan timeout, Func<AsyncCallback?, object?, IAsyncResult> executeSend, AsyncCallback? callback, object? state)
    {
        IAsyncResult? result = null;
        this.SendInternal(
            message,
            timeout,
            telemetryState =>
            {
                var asyncCallback = AsyncResultWithTelemetryState.GetAsyncCallback(callback, telemetryState);
                result = new AsyncResultWithTelemetryState(executeSend(asyncCallback, state), telemetryState);
            },
            completeOneWay: false);
        return result!;
    }

    private void SendInternal(
        Message message,
        TimeSpan timeout,
        Action<RequestTelemetryState> executeSend,
        bool completeOneWay = true)
    {
        RequestTelemetryState? telemetryState = null;

        void ExecuteInChildContext(object? unused)
        {
            telemetryState = ClientChannelInstrumentation.BeforeSendRequest(message, this.RemoteAddress?.Uri);
            if (!telemetryState.IsOneWay)
            {
                telemetryState.Owner = this;
                RequestTelemetryStateTracker.PushTelemetryState(message, telemetryState, timeout, OnTelemetryStateTimedOut);
            }

            executeSend(telemetryState);

            if (completeOneWay)
            {
                // A synchronous send has finished. An asynchronous one finishes in EndSend.
                if (telemetryState.IsOneWay)
                {
                    ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState);
                }
                else
                {
                    OnSendCompleted(telemetryState);
                }
            }
        }

        try
        {
            var executionContext = ExecutionContext.Capture();
            if (executionContext == null)
            {
                ExecuteInChildContext(null);
            }
            else
            {
                ExecutionContext.Run(executionContext, ExecuteInChildContext, null);
            }
        }
        catch (Exception ex)
        {
            RequestTelemetryStateTracker.RemoveTelemetryState(telemetryState);
            ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState, ex);
            throw;
        }
    }
}

// <copyright file="InstrumentedDuplexChannel.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class InstrumentedDuplexChannel : InstrumentedChannel<IDuplexChannel>, IDuplexSessionChannel
{
    private readonly TimeSpan telemetryTimeout;

    public InstrumentedDuplexChannel(IDuplexChannel inner, TimeSpan telemetryTimeout)
        : base(inner)
    {
        this.telemetryTimeout = telemetryTimeout;
    }

    public EndpointAddress LocalAddress => this.Inner.LocalAddress;

    public EndpointAddress RemoteAddress => this.Inner.RemoteAddress;

    public Uri Via => this.Inner.Via;

    public IDuplexSession Session => ((IDuplexSessionChannel)this.Inner).Session;

    public void Send(Message message)
    {
        this.SendInternal(message, this.telemetryTimeout, _ => this.Inner.Send(message));
    }

    public void Send(Message message, TimeSpan timeout)
    {
        this.SendInternal(message, timeout, _ => this.Inner.Send(message, timeout));
    }

    public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
    {
        return this.SendInternal(message, this.telemetryTimeout, (cb, s) => this.Inner.BeginSend(message, cb, s), callback, state);
    }

    public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.SendInternal(message, timeout, (cb, s) => this.Inner.BeginSend(message, timeout, cb, s), callback, state);
    }

    public void EndSend(IAsyncResult result)
    {
        var asyncResult = (AsyncResultWithTelemetryState)result;
        try
        {
            this.Inner.EndSend(asyncResult.Inner);
        }
        catch (Exception)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(null, asyncResult.TelemetryState);
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

    public IAsyncResult BeginReceive(AsyncCallback callback, object state)
    {
        return this.Inner.BeginReceive(callback, state);
    }

    public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.Inner.BeginReceive(timeout, callback, state);
    }

    public Message EndReceive(IAsyncResult result)
    {
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

    public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.Inner.BeginTryReceive(timeout, callback, state);
    }

    public bool EndTryReceive(IAsyncResult result, out Message message)
    {
        var returnValue = this.Inner.EndTryReceive(result, out message);
        HandleReceivedMessage(message);
        return returnValue;
    }

    public bool WaitForMessage(TimeSpan timeout)
    {
        return this.Inner.WaitForMessage(timeout);
    }

    public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.Inner.BeginWaitForMessage(timeout, callback, state);
    }

    public bool EndWaitForMessage(IAsyncResult result)
    {
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

    private IAsyncResult SendInternal(Message message, TimeSpan timeout, Func<AsyncCallback, object, IAsyncResult> executeSend, AsyncCallback callback, object state)
    {
        IAsyncResult result = null;
        this.SendInternal(message, timeout, telemetryState =>
        {
            var asyncCallback = AsyncResultWithTelemetryState.GetAsyncCallback(callback, telemetryState);
            result = new AsyncResultWithTelemetryState(executeSend(asyncCallback, state), telemetryState);
        });
        return result;
    }

    private void SendInternal(Message message, TimeSpan timeout, Action<RequestTelemetryState> executeSend)
    {
        RequestTelemetryState telemetryState = null;
        ContextCallback executeInChildContext = _ =>
        {
            telemetryState = ClientChannelInstrumentation.BeforeSendRequest(message, this.RemoteAddress?.Uri);
            RequestTelemetryStateTracker.PushTelemetryState(message, telemetryState, timeout, OnTelemetryStateTimedOut);
            executeSend(telemetryState);
        };

        try
        {
            ExecutionContext.Run(ExecutionContext.Capture(), executeInChildContext, null);
        }
        catch (Exception)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState);
            throw;
        }
    }
}

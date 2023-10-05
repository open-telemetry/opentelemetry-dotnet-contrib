// <copyright file="InstrumentedRequestChannel.cs" company="OpenTelemetry Authors">
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

internal sealed class InstrumentedRequestChannel : InstrumentedChannel<IRequestChannel>, IRequestSessionChannel
{
    public InstrumentedRequestChannel(IRequestChannel inner)
        : base(inner)
    {
    }

    EndpointAddress IRequestChannel.RemoteAddress => this.Inner.RemoteAddress;

    Uri IRequestChannel.Via => this.Inner.Via;

    IOutputSession ISessionChannel<IOutputSession>.Session => ((ISessionChannel<IOutputSession>)this.Inner).Session;

    Message IRequestChannel.Request(Message message)
    {
        var telemetryState = ClientChannelInstrumentation.BeforeSendRequest(message, ((IRequestChannel)this).RemoteAddress?.Uri);
        Message reply;
        try
        {
            reply = this.Inner.Request(message);
        }
        catch (Exception)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState);
            throw;
        }

        ClientChannelInstrumentation.AfterRequestCompleted(reply, telemetryState);
        return reply;
    }

    Message IRequestChannel.Request(Message message, TimeSpan timeout)
    {
        var telemetryState = ClientChannelInstrumentation.BeforeSendRequest(message, ((IRequestChannel)this).RemoteAddress?.Uri);
        Message reply;
        try
        {
            reply = this.Inner.Request(message, timeout);
        }
        catch (Exception)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(null, telemetryState);
            throw;
        }

        ClientChannelInstrumentation.AfterRequestCompleted(reply, telemetryState);
        return reply;
    }

    IAsyncResult IRequestChannel.BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.InternalBeginRequest(message, (cb, s) => this.Inner.BeginRequest(message, timeout, cb, s), callback, state);
    }

    IAsyncResult IRequestChannel.BeginRequest(Message message, AsyncCallback callback, object state)
    {
        return this.InternalBeginRequest(message, (cb, s) => this.Inner.BeginRequest(message, cb, s), callback, state);
    }

    Message IRequestChannel.EndRequest(IAsyncResult result)
    {
        var asyncResult = (AsyncResultWithTelemetryState)result;
        Message reply;
        try
        {
            reply = this.Inner.EndRequest(asyncResult.Inner);
        }
        catch (Exception)
        {
            ClientChannelInstrumentation.AfterRequestCompleted(null, asyncResult.TelemetryState);
            throw;
        }

        ClientChannelInstrumentation.AfterRequestCompleted(reply, asyncResult.TelemetryState);
        return reply;
    }

    private IAsyncResult InternalBeginRequest(Message message, Func<AsyncCallback, object, IAsyncResult> beginRequestDelegate, AsyncCallback callback, object state)
    {
        IAsyncResult result = null;
        ContextCallback executeInChildContext = _ =>
        {
            var telemetryState = ClientChannelInstrumentation.BeforeSendRequest(message, ((IRequestChannel)this).RemoteAddress?.Uri);
            var asyncCallback = AsyncResultWithTelemetryState.GetAsyncCallback(callback, telemetryState);
            result = new AsyncResultWithTelemetryState(beginRequestDelegate(asyncCallback, state), telemetryState);
        };

        ExecutionContext.Run(ExecutionContext.Capture(), executeInChildContext, null);
        return result;
    }
}

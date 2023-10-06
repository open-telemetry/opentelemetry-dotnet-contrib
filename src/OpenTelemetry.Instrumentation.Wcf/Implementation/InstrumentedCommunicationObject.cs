// <copyright file="InstrumentedCommunicationObject.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class InstrumentedCommunicationObject<T> : ICommunicationObject
    where T : ICommunicationObject
{
    public InstrumentedCommunicationObject(T inner)
    {
        this.Inner = inner;
    }

    event EventHandler ICommunicationObject.Closed { add => this.Inner.Closed += value; remove => this.Inner.Closed -= value; }

    event EventHandler ICommunicationObject.Closing { add => this.Inner.Closing += value; remove => this.Inner.Closing -= value; }

    event EventHandler ICommunicationObject.Faulted { add => this.Inner.Faulted += value; remove => this.Inner.Faulted -= value; }

    event EventHandler ICommunicationObject.Opened { add => this.Inner.Opened += value; remove => this.Inner.Opened -= value; }

    event EventHandler ICommunicationObject.Opening { add => this.Inner.Opening += value; remove => this.Inner.Opening -= value; }

    CommunicationState ICommunicationObject.State
    {
        get { return this.Inner.State; }
    }

    protected T Inner { get; }

    void ICommunicationObject.Abort()
    {
        this.Inner.Abort();
    }

    IAsyncResult ICommunicationObject.BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.Inner.BeginClose(timeout, callback, state);
    }

    IAsyncResult ICommunicationObject.BeginClose(AsyncCallback callback, object state)
    {
        return this.Inner.BeginClose(callback, state);
    }

    IAsyncResult ICommunicationObject.BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
    {
        return this.Inner.BeginOpen(timeout, callback, state);
    }

    IAsyncResult ICommunicationObject.BeginOpen(AsyncCallback callback, object state)
    {
        return this.Inner.BeginOpen(callback, state);
    }

    void ICommunicationObject.Close(TimeSpan timeout)
    {
        this.Inner.Close(timeout);
    }

    void ICommunicationObject.Close()
    {
        this.Inner.Close();
    }

    void ICommunicationObject.EndClose(IAsyncResult result)
    {
        this.Inner.EndClose(result);
    }

    void ICommunicationObject.EndOpen(IAsyncResult result)
    {
        this.Inner.EndOpen(result);
    }

    void ICommunicationObject.Open(TimeSpan timeout)
    {
        this.Inner.Open(timeout);
    }

    void ICommunicationObject.Open()
    {
        this.Inner.Open();
    }
}

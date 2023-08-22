// <copyright file="AsyncResultWithTelemetryState.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class AsyncResultWithTelemetryState : IAsyncResult
{
    public AsyncResultWithTelemetryState(IAsyncResult inner, RequestTelemetryState telemetryState)
    {
        this.Inner = inner;
        this.TelemetryState = telemetryState;
    }

    public IAsyncResult Inner { get; }

    public RequestTelemetryState TelemetryState { get; }

    object IAsyncResult.AsyncState => this.Inner.AsyncState;

    WaitHandle IAsyncResult.AsyncWaitHandle => this.Inner.AsyncWaitHandle;

    bool IAsyncResult.CompletedSynchronously => this.Inner.CompletedSynchronously;

    bool IAsyncResult.IsCompleted => this.Inner.IsCompleted;

    public static AsyncCallback GetAsyncCallback(AsyncCallback innerCallback, RequestTelemetryState telemetryState)
    {
        return (IAsyncResult ar) =>
        {
            innerCallback(new AsyncResultWithTelemetryState(ar, telemetryState));
        };
    }
}

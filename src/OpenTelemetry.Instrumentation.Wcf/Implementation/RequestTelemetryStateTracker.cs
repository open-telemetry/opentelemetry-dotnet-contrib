// <copyright file="RequestTelemetryStateTracker.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class RequestTelemetryStateTracker
{
    private Dictionary<string, RequestTelemetryState> state = new Dictionary<string, RequestTelemetryState>();
    private Queue<string> messageIdQueue = new Queue<string>();
    private object sync = new object();
    private int maxTrackedRequestCount;

    public RequestTelemetryStateTracker(int maxTrackedRequestCount)
    {
        this.maxTrackedRequestCount = maxTrackedRequestCount;
    }

    public void PushTelemetryState(Message request, RequestTelemetryState telemetryState)
    {
        var msgId = request?.Headers.MessageId?.ToString();
        if (msgId != null)
        {
            lock (this.sync)
            {
                while (this.state.Count >= this.maxTrackedRequestCount || this.messageIdQueue.Count >= this.maxTrackedRequestCount)
                {
                    this.state.Remove(this.messageIdQueue.Dequeue());
                }

                this.state[msgId] = telemetryState;
                this.messageIdQueue.Enqueue(msgId);
            }
        }
    }

    public RequestTelemetryState PopTelemetryState(Message reply)
    {
        RequestTelemetryState returnValue = null;
        var relatesTo = reply?.Headers.RelatesTo?.ToString();
        if (relatesTo != null)
        {
            lock (this.sync)
            {
                if (this.state.TryGetValue(relatesTo, out var telemetryState))
                {
                    this.state.Remove(relatesTo);
                    returnValue = telemetryState;
                }

                while (this.messageIdQueue.Count > 0 && !this.state.ContainsKey(this.messageIdQueue.Peek()))
                {
                    this.messageIdQueue.Dequeue();
                }
            }
        }

        return returnValue;
    }
}

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

using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Threading;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class RequestTelemetryStateTracker
{
    private Dictionary<string, Entry> outstandingRequestStates = new Dictionary<string, Entry>();
    private int timeout;

    public RequestTelemetryStateTracker(TimeSpan timeout)
    {
        if (timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be less than 2^31 milliseconds");
        }

        this.timeout = (int)timeout.TotalMilliseconds;
    }

    public event EventHandler<RequestTelemetryState> TelemetryStateTimedOut;

    public void PushTelemetryState(Message request, RequestTelemetryState telemetryState)
    {
        var messageId = request?.Headers.MessageId?.ToString();
        if (messageId != null)
        {
            var entry = new Entry(telemetryState);
            lock (this.outstandingRequestStates)
            {
                this.outstandingRequestStates[messageId] = entry;
            }

            entry.TimeoutTimer = new Timer(this.OnTimer, messageId, this.timeout, Timeout.Infinite);
        }
    }

    public RequestTelemetryState PopTelemetryState(Message reply)
    {
        var relatesTo = reply?.Headers.RelatesTo?.ToString();
        return relatesTo == null ? null : this.PopTelemetryState(relatesTo);
    }

    private RequestTelemetryState PopTelemetryState(string messageId)
    {
        Entry entry = null;
        lock (this.outstandingRequestStates)
        {
            if (this.outstandingRequestStates.TryGetValue(messageId, out entry))
            {
                this.outstandingRequestStates.Remove(messageId);
            }
        }

        entry?.TimeoutTimer.Dispose();
        return entry?.State;
    }

    private void OnTimer(object state)
    {
        var messageId = (string)state;
        var telemetryState = this.PopTelemetryState(messageId);
        if (telemetryState != null)
        {
            var handlers = this.TelemetryStateTimedOut;
            if (handlers != null)
            {
                handlers(this, telemetryState);
            }
        }
    }

    private class Entry
    {
        public Timer TimeoutTimer;
        public RequestTelemetryState State;

        public Entry(RequestTelemetryState state)
        {
            this.State = state;
        }
    }
}

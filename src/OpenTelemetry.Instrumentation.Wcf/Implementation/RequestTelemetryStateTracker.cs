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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ServiceModel.Channels;
using System.Threading;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class RequestTelemetryStateTracker : IDisposable
{
    private readonly OrderedDictionary outstandingRequestStates = new OrderedDictionary();
    private readonly object sync = new object();
    private Timer timer;

    private long configuredTimeoutInMs;

    public RequestTelemetryStateTracker(TimeSpan timeout)
    {
        this.configuredTimeoutInMs = (long)timeout.TotalMilliseconds;
    }

    public event EventHandler<RequestTelemetryState> TelemetryStateTimedOut;

    public void PushTelemetryState(Message request, RequestTelemetryState telemetryState)
    {
        var messageId = request?.Headers.MessageId?.ToString();
        if (messageId != null)
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + this.configuredTimeoutInMs;
            var entry = new Entry(telemetryState, expiresAt);
            lock (this.sync)
            {
                this.outstandingRequestStates.Add(messageId, entry);
                if (this.timer == null)
                {
                    this.SetTimer(expiresAt);
                }
            }
        }
    }

    public RequestTelemetryState PopTelemetryState(Message reply)
    {
        var relatesTo = reply?.Headers.RelatesTo?.ToString();
        return relatesTo == null ? null : this.PopTelemetryState(relatesTo);
    }

    public void Dispose()
    {
        this.SetTimer(null);

        Entry[] abandonedEntries = null;
        lock (this.sync)
        {
            abandonedEntries = new Entry[this.outstandingRequestStates.Count];
            this.outstandingRequestStates.Values.CopyTo(abandonedEntries, 0);
            this.outstandingRequestStates.Clear();
        }

        foreach (Entry entry in abandonedEntries)
        {
            this.TriggerTimedOutEvent(entry);
        }
    }

    private RequestTelemetryState PopTelemetryState(string messageId)
    {
        lock (this.sync)
        {
            var entry = (Entry)this.outstandingRequestStates[messageId];
            if (entry != null)
            {
                this.outstandingRequestStates.Remove(messageId);
            }

            return entry?.State;
        }
    }

    private void OnTimer(object state)
    {
        List<DictionaryEntry> timedOut = null;
        lock (this.sync)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long? nextExpiry = null;
            foreach (DictionaryEntry kvp in this.outstandingRequestStates)
            {
                if (((Entry)kvp.Value).ExpiresAt <= now)
                {
                    if (timedOut == null)
                    {
                        timedOut = new List<DictionaryEntry>();
                    }

                    timedOut.Add(kvp);
                }
                else
                {
                    nextExpiry = ((Entry)kvp.Value).ExpiresAt;
                    break;
                }
            }

            timedOut?.ForEach(kvp => this.outstandingRequestStates.Remove(kvp.Key));
            this.SetTimer(nextExpiry);
        }

        timedOut?.ForEach(kvp => this.TriggerTimedOutEvent((Entry)kvp.Value));
    }

    private void SetTimer(long? dueAt)
    {
        if (dueAt.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var dueIn = Math.Max(0, dueAt.Value - now) + 50; // add 50ms here because the timer is not precise and often fires just a bit early
            this.timer?.Dispose();
            this.timer = new Timer(this.OnTimer, null, dueIn, Timeout.Infinite);
        }
        else
        {
            this.timer?.Dispose();
            this.timer = null;
        }
    }

    private void TriggerTimedOutEvent(Entry entry)
    {
        var handlers = this.TelemetryStateTimedOut;
        handlers?.Invoke(this, entry.State);
    }

    private class Entry
    {
        public readonly RequestTelemetryState State;
        public readonly long ExpiresAt;

        public Entry(RequestTelemetryState state, long expiresAt)
        {
            this.State = state;
            this.ExpiresAt = expiresAt;
        }
    }
}

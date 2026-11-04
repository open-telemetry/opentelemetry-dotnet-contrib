// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal static class RequestTelemetryStateTracker
{
    // Largest due time Timer.Change accepts (uint.MaxValue - 1 ms, about 49 days). A request can expire
    // later than that (e.g. an infinite WCF timeout); the timer then fires early and OnTimer re-arms it.
    private const long MaxTimerDueIn = 0xFFFFFFFE;

    private static readonly Dictionary<string, Entry> OutstandingRequestStates = [];
    private static readonly SortedSet<EntryTimeoutProperties> TimeoutQueue = [];
    private static readonly Dictionary<object, HashSet<string>> RequestsByOwner = [];
    private static readonly Lock Sync = new();
    private static readonly Timer Timer = new(OnTimer);
    private static long currentTimerDueAt = Timeout.Infinite;

    public static void PushTelemetryState(Message request, RequestTelemetryState telemetryState, TimeSpan timeout, Action<RequestTelemetryState> timeoutCallback)
    {
        var messageId = request?.Headers.MessageId?.ToString();
        if (messageId != null)
        {
            telemetryState.MessageId = messageId;
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)timeout.TotalMilliseconds;
            var timeoutProps = new EntryTimeoutProperties(messageId, expiresAt, timeoutCallback);
            var entry = new Entry(telemetryState, timeoutProps);
            lock (Sync)
            {
                OutstandingRequestStates.Add(messageId, entry);
                TimeoutQueue.Add(timeoutProps);
                AddToOwnerIndex(messageId, telemetryState.Owner);
                SetTimerEarlierIfNeeded(expiresAt);
            }
        }
    }

    public static RequestTelemetryState? PopTelemetryState(Message reply)
    {
        var relatesTo = reply?.Headers.RelatesTo?.ToString();
        return relatesTo == null ? null : PopTelemetryState(relatesTo);
    }

    public static List<RequestTelemetryState> RemoveTelemetryStates(object owner)
    {
        var removed = new List<RequestTelemetryState>();

        lock (Sync)
        {
            // Only the requests of this owner are visited, regardless of how many are outstanding overall.
            if (RequestsByOwner.TryGetValue(owner, out var messageIds))
            {
                RequestsByOwner.Remove(owner);

                foreach (var messageId in messageIds)
                {
                    var entry = OutstandingRequestStates[messageId];

                    OutstandingRequestStates.Remove(messageId);
                    TimeoutQueue.Remove(entry.TimeoutProperties);
                    removed.Add(entry.State);
                }
            }
        }

        return removed;
    }

    public static void RemoveTelemetryState(RequestTelemetryState? telemetryState)
    {
        var messageId = telemetryState?.MessageId;
        if (messageId == null)
        {
            return;
        }

        lock (Sync)
        {
            // Only remove the entry if it still belongs to this state: a reply may have popped it
            // already, and a later request could reuse the id.
            if (OutstandingRequestStates.TryGetValue(messageId, out var entry)
                && ReferenceEquals(entry.State, telemetryState))
            {
                RemoveEntry(messageId, entry);
            }
        }
    }

    private static RequestTelemetryState? PopTelemetryState(string messageId)
    {
        lock (Sync)
        {
            if (OutstandingRequestStates.TryGetValue(messageId, out var entry))
            {
                RemoveEntry(messageId, entry);
            }

            return entry?.State;
        }
    }

    private static void OnTimer(object? state)
    {
        try
        {
            OnTimerCore();
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.TelemetryTimeoutException(ex);
        }
    }

    private static void OnTimerCore()
    {
        List<Tuple<EntryTimeoutProperties, Entry>>? timedOutEntries = null;
        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            EntryTimeoutProperties? nextToExpire = null;
            foreach (var entryTimeoutProps in TimeoutQueue)
            {
                if (entryTimeoutProps.ExpiresAt <= now)
                {
                    timedOutEntries ??= [];

                    timedOutEntries.Add(new(entryTimeoutProps, OutstandingRequestStates[entryTimeoutProps.MessageId]));
                }
                else
                {
                    nextToExpire = entryTimeoutProps;
                    break;
                }
            }

            foreach (var entry in timedOutEntries ?? Enumerable.Empty<Tuple<EntryTimeoutProperties, Entry>>())
            {
                RemoveEntry(entry.Item1.MessageId, entry.Item2);
            }

            // when there's no more outstanding requests we set time timer to infinite, effectively disabling it until another request arrives
            var nextExpiry = nextToExpire?.ExpiresAt ?? Timeout.Infinite;
            SetTimer(nextExpiry);
        }

        if (timedOutEntries != null)
        {
            foreach (var entry in timedOutEntries)
            {
                try
                {
                    entry.Item1.TimeoutCallback(entry.Item2.State);
                }
                catch (Exception ex)
                {
                    // A failing callback must not prevent the remaining requests from being completed.
                    WcfInstrumentationEventSource.Log.TelemetryTimeoutException(ex);
                }
            }
        }
    }

    private static void AddToOwnerIndex(string messageId, object? owner)
    {
        if (owner == null)
        {
            return;
        }

        if (!RequestsByOwner.TryGetValue(owner, out var messageIds))
        {
            RequestsByOwner[owner] = messageIds = [];
        }

        messageIds.Add(messageId);
    }

    private static void RemoveEntry(string messageId, Entry entry)
    {
        OutstandingRequestStates.Remove(messageId);
        TimeoutQueue.Remove(entry.TimeoutProperties);

        var owner = entry.State.Owner;
        if (owner != null && RequestsByOwner.TryGetValue(owner, out var messageIds))
        {
            messageIds.Remove(messageId);
            if (messageIds.Count == 0)
            {
                RequestsByOwner.Remove(owner);
            }
        }
    }

    private static void SetTimerEarlierIfNeeded(long dueAt)
    {
        if (currentTimerDueAt == Timeout.Infinite || currentTimerDueAt > dueAt)
        {
            SetTimer(dueAt);
        }
    }

    private static void SetTimer(long dueAt)
    {
        long dueIn;
        if (dueAt == Timeout.Infinite)
        {
            dueIn = Timeout.Infinite;
        }
        else
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            dueIn = Math.Max(0, dueAt - now) + 50; // add 50ms here because the timer is not precise and often fires just a bit early
            dueIn = Math.Min(dueIn, MaxTimerDueIn);
        }

        Timer.Change(dueIn, Timeout.Infinite);
        currentTimerDueAt = dueAt;
    }

    private class Entry
    {
        public readonly RequestTelemetryState State;
        public readonly EntryTimeoutProperties TimeoutProperties;

        public Entry(RequestTelemetryState state, EntryTimeoutProperties timeoutProperties)
        {
            this.State = state;
            this.TimeoutProperties = timeoutProperties;
        }
    }

    private class EntryTimeoutProperties : IComparable
    {
        public readonly string MessageId;
        public readonly long ExpiresAt;
        public readonly Action<RequestTelemetryState> TimeoutCallback;

        public EntryTimeoutProperties(string messageId, long expiresAt, Action<RequestTelemetryState> timeoutCallback)
        {
            this.MessageId = messageId;
            this.ExpiresAt = expiresAt;
            this.TimeoutCallback = timeoutCallback;
        }

        public int CompareTo(object? obj)
        {
            if (obj is not EntryTimeoutProperties other)
            {
                return 1;
            }

            var result = this.ExpiresAt.CompareTo(other.ExpiresAt);
            if (result == 0)
            {
                result = string.Compare(this.MessageId, other.MessageId, StringComparison.Ordinal);
            }

            return result;
        }
    }
}

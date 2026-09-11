// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal static class EventListenerExtensions
{
    public static async Task<EventWrittenEventArgs> WaitForEventAsync(
        this InMemoryEventListener eventListener,
        Func<EventWrittenEventArgs, bool> predicate,
        string eventDescription,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            while (eventListener.Events.TryDequeue(out var candidate))
            {
                if (predicate(candidate))
                {
                    return candidate;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for event '{eventDescription}'.");
    }
}

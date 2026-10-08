// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.StackExchangeRedis;

internal static class RedisDrainInterval
{
    private const int MinDrainIntervalMilliseconds = 100;

    internal static int GetNext(int current, int minimum, int maximum, bool drainedCommands)
        => drainedCommands ? minimum : (int)Math.Min(current * 2L, maximum);

    internal static int GetMinimum(int flushIntervalMilliseconds)
        => Math.Min(MinDrainIntervalMilliseconds, flushIntervalMilliseconds);

    internal static void SignalIfBackedOff(EventWaitHandle workAvailableHandle, int currentInterval, int minimumInterval)
    {
        if (currentInterval <= minimumInterval)
        {
            return;
        }

        try
        {
            workAvailableHandle.Set();
        }
        catch (ObjectDisposedException)
        {
            // A profiler factory invocation can race with disposal after observing
            // disposed == 0 above. The session is still returned safely.
        }
    }
}

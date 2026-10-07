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
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

[Collection("Redis")]
public class StackExchangeRedisInstrumentationOptionsTests
{
    [Fact]
    public void EnableEarlyCommandDrain_TruthTable()
    {
        Assert.True(new StackExchangeRedisInstrumentationOptions().EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Filter = _ => true,
        }.EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Enrich = (_, _) => { },
        }.EnableEarlyCommandDrain);

        Assert.False(new StackExchangeRedisInstrumentationOptions
        {
            Filter = _ => true,
            Enrich = (_, _) => { },
        }.EnableEarlyCommandDrain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    public void FlushInterval_NonPositive_Throws(double milliseconds)
    {
        var options = new StackExchangeRedisInstrumentationOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.FlushInterval = TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FlushInterval_SubMillisecond_Throws()
    {
        var options = new StackExchangeRedisInstrumentationOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.FlushInterval = TimeSpan.FromTicks(1));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FlushInterval_OneMillisecond_IsAccepted()
    {
        var options = new StackExchangeRedisInstrumentationOptions
        {
            FlushInterval = TimeSpan.FromMilliseconds(1),
        };

        Assert.Equal(TimeSpan.FromMilliseconds(1), options.FlushInterval);
        Assert.Equal(1, RedisDrainInterval.GetMinimum((int)options.FlushInterval.TotalMilliseconds));
    }

    [Fact]
    public void FlushInterval_ExceedsWaitOneLimit_Throws()
    {
        var options = new StackExchangeRedisInstrumentationOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.FlushInterval = TimeSpan.FromMilliseconds((double)int.MaxValue + 1));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FlushInterval_MaximumWaitOneLimit_IsAccepted()
    {
        var options = new StackExchangeRedisInstrumentationOptions
        {
            FlushInterval = TimeSpan.FromMilliseconds(int.MaxValue),
        };

        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), options.FlushInterval);
    }

    [Fact]
    public void DrainInterval_EmptyPoll_DoublesUpToMaximum()
    {
        Assert.Equal(200, RedisDrainInterval.GetNext(100, 100, 1_000, drainedCommands: false));
        Assert.Equal(400, RedisDrainInterval.GetNext(200, 100, 1_000, drainedCommands: false));
        Assert.Equal(1_000, RedisDrainInterval.GetNext(800, 100, 1_000, drainedCommands: false));
        Assert.Equal(1_000, RedisDrainInterval.GetNext(1_000, 100, 1_000, drainedCommands: false));
    }

    [Fact]
    public void DrainInterval_EmptyPoll_NearIntMaxValue_SaturatesAtMaximum()
    {
        Assert.Equal(int.MaxValue, RedisDrainInterval.GetNext((int.MaxValue / 2) + 1, 100, int.MaxValue, drainedCommands: false));
    }

    [Fact]
    public void DrainInterval_FlushBelowMinimum_UsesFlushIntervalAsMinimumAndMaximum()
    {
        var flushInterval = 25;

        Assert.Equal(flushInterval, RedisDrainInterval.GetMinimum(flushInterval));
        Assert.Equal(flushInterval, RedisDrainInterval.GetNext(flushInterval, flushInterval, flushInterval, drainedCommands: false));
        Assert.Equal(flushInterval, RedisDrainInterval.GetNext(flushInterval, flushInterval, flushInterval, drainedCommands: true));
    }

    [Fact]
    public void DrainInterval_FindingCommandsResetsToMinimum()
    {
        Assert.Equal(100, RedisDrainInterval.GetNext(800, 100, 1_000, drainedCommands: true));
    }

    [Fact]
    public void DrainSignal_AtMinimumInterval_IsUnnecessary()
    {
        using var workAvailable = new EventWaitHandle(false, EventResetMode.AutoReset);

        RedisDrainInterval.SignalIfBackedOff(workAvailable, currentInterval: 100, minimumInterval: 100);

        Assert.False(workAvailable.WaitOne(0));
    }

    [Fact]
    public void DrainSignal_AfterHandleDisposed_ExitsCleanly()
    {
        var workAvailable = new EventWaitHandle(false, EventResetMode.AutoReset);
        workAvailable.Dispose();

        Assert.Null(Record.Exception(() => RedisDrainInterval.SignalIfBackedOff(workAvailable, currentInterval: 200, minimumInterval: 100)));
    }
}

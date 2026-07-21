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
}

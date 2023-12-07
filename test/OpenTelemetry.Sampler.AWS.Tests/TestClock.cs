// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace OpenTelemetry.Sampler.AWS.Tests;

internal class TestClock : Clock
{
    private static readonly DateTime EpochStart = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime nowTime;

    public TestClock()
    {
        this.nowTime = DateTime.Now;
    }

    public TestClock(DateTime time)
    {
        this.nowTime = time;
    }

    public override DateTime Now()
    {
        return this.nowTime;
    }

    public override long NowInMilliSeconds()
    {
        return (long)this.nowTime.ToUniversalTime().Subtract(EpochStart).TotalMilliseconds;
    }

    public override DateTime ToDateTime(double seconds)
    {
        return EpochStart.AddSeconds(seconds);
    }

    public override double ToDouble(DateTime dateTime)
    {
        TimeSpan current = new TimeSpan(dateTime.ToUniversalTime().Ticks - EpochStart.Ticks);
        double timestamp = Math.Round(current.TotalMilliseconds, 0) / 1000.0;
        return timestamp;
    }

    // Advances the clock by a given time span.
    public void Advance(TimeSpan time)
    {
        this.nowTime = this.nowTime.Add(time);
    }
}

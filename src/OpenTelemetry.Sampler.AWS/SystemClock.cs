// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace OpenTelemetry.Sampler.AWS;

// A clock based on System time.
internal class SystemClock : Clock
{
    private static readonly SystemClock Instance = new SystemClock();

    private static readonly DateTime EpochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private SystemClock()
    {
    }

    public static Clock GetInstance()
    {
        return Instance;
    }

    public override DateTime Now()
    {
        return DateTime.UtcNow;
    }

    public override long NowInMilliSeconds()
    {
        return (long)this.Now().ToUniversalTime().Subtract(EpochStart).TotalMilliseconds;
    }

    public override DateTime ToDateTime(double seconds)
    {
        return EpochStart.AddSeconds(seconds);
    }

    public override double ToDouble(DateTime dateTime)
    {
        var current = new TimeSpan(dateTime.ToUniversalTime().Ticks - EpochStart.Ticks);
        double timestamp = Math.Round(current.TotalMilliseconds, 0) / 1000.0;
        return timestamp;
    }
}

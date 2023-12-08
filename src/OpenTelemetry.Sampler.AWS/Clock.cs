// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;

namespace OpenTelemetry.Sampler.AWS;

// A time keeper for the purpose of this sampler.
internal abstract class Clock
{
    public static Clock GetDefault()
    {
        return SystemClock.GetInstance();
    }

    public abstract DateTime Now();

    public abstract long NowInMilliSeconds();

    public abstract DateTime ToDateTime(double seconds);

    public abstract double ToDouble(DateTime dateTime);
}

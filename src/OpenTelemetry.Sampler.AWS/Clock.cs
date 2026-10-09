// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS;

// A time keeper for the purpose of this sampler.
internal abstract class Clock
{
    // The Unix times, in seconds, of DateTimeOffset.MinValue and DateTimeOffset.MaxValue.
    private const double MinUnixTimeSeconds = -62_135_596_800;
    private const double MaxUnixTimeSeconds = 253_402_300_799;

    public static Clock GetDefault()
        => SystemClock.GetInstance();

    public static bool IsValidUnixTime(double seconds) =>
        seconds is >= MinUnixTimeSeconds and <= MaxUnixTimeSeconds;

    public abstract DateTimeOffset Now();

    public abstract long NowInMilliSeconds();

    public abstract DateTimeOffset ToDateTime(double seconds);

    public abstract double ToDouble(DateTimeOffset dateTime);
}

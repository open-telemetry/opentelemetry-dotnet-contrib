// <copyright file="SystemClock.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

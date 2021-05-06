// <copyright file="DateTimeExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    internal static class DateTimeExtensions
    {
        private static readonly long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private static readonly long UnixEpochTicks = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks;
        private static readonly long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

        internal static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        internal static long ToEpochMicroseconds(this TimeSpan timeSpan)
        {
            return timeSpan.Ticks / TicksPerMicrosecond;
        }
    }
}

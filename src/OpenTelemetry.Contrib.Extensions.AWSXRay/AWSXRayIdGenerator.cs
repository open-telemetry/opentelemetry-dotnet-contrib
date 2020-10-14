// <copyright file="AWSXRayIdGenerator.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay
{
    /// <summary>
    /// Generate AWS X-Ray compatible trace id and replace the trace id of root activity.
    /// See https://docs.aws.amazon.com/xray/latest/devguide/xray-api-sendingdata.html#xray-api-traceids.
    /// </summary>
    public static class AWSXRayIdGenerator
    {
        private const int RandomNumberHexDigits = 24;

        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private const long MicrosecondPerSecond = TimeSpan.TicksPerSecond / TicksPerMicrosecond;

        private static readonly DateTime EpochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly long UnixEpochMicroseconds = EpochStart.Ticks / TicksPerMicrosecond;
        private static readonly Random Global = new Random();
        private static readonly ThreadLocal<Random> Local = new ThreadLocal<Random>(() =>
        {
            int seed;
            lock (Global)
            {
                seed = Global.Next();
            }

            return new Random(seed);
        });

        internal static void ReplaceTraceId()
        {
            var awsXRayActivityListener = new ActivityListener
            {
                ActivityStarted = (activity) =>
                {
                    if (string.IsNullOrEmpty(activity.ParentId))
                    {
                        var awsXRayTraceId = GenerateAWSXRayCompatiableTraceId();

                        activity.SetParentId(awsXRayTraceId, default, activity.ActivityTraceFlags);
                    }
                },

                ShouldListenTo = (_) => true,
            };

            ActivitySource.AddActivityListener(awsXRayActivityListener);
        }

        internal static ActivityTraceId GenerateAWSXRayCompatiableTraceId()
        {
            var epoch = (int)DateTime.UtcNow.ToUnixTimeSeconds(); // first 8 digit as time stamp

            var randomNumber = GenerateHexNumber(RandomNumberHexDigits); // remaining 24 random digit

            var newTraceId = string.Concat(epoch.ToString("x", CultureInfo.InvariantCulture), randomNumber);

            return ActivityTraceId.CreateFromString(newTraceId.AsSpan());
        }

        /// <summary>
        /// Convert a given time to Unix time which is the number of seconds since 1st January 1970, 00:00:00 UTC.
        /// </summary>
        /// <param name="date">.Net representation of time.</param>
        /// <returns>The number of seconds elapsed since 1970-01-01 00:00:00 UTC. The value is expressed in whole and fractional seconds with resolution of microsecond.</returns>
        private static decimal ToUnixTimeSeconds(this DateTime date)
        {
            long microseconds = date.Ticks / TicksPerMicrosecond;
            long microsecondsSinceEpoch = microseconds - UnixEpochMicroseconds;
            return (decimal)microsecondsSinceEpoch / MicrosecondPerSecond;
        }

        /// <summary>
        /// Generate a random 24-digit hex number.
        /// </summary>
        /// <param name="digits">Digits of the hex number.</param>
        /// <returns>The generated hex number.</returns>
        private static string GenerateHexNumber(int digits)
        {
            if (digits < 0)
            {
                throw new ArgumentException("Length can't be a negative number.", "digits");
            }

            byte[] bytes = new byte[digits / 2];
            NextBytes(bytes);
            string hexNumber = string.Concat(bytes.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)).ToArray());
            if (digits % 2 != 0)
            {
                hexNumber += Next(16).ToString("x", CultureInfo.InvariantCulture);
            }

            return hexNumber;
        }

        /// <summary>
        /// Fills the elements of a specified array of bytes with random numbers.
        /// </summary>
        /// <param name="buffer">An array of bytes to contain random numbers.</param>
        private static void NextBytes(byte[] buffer)
        {
            Local.Value.NextBytes(buffer);
        }

        /// <summary>
        /// Returns a non-negative random integer that is less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">Max value of the random integer.</param>
        /// <returns>A 32-bit signed integer that is greater than or equal to 0, and less than maxValue.</returns>
        private static int Next(int maxValue)
        {
            return Local.Value.Next(maxValue);
        }
    }
}

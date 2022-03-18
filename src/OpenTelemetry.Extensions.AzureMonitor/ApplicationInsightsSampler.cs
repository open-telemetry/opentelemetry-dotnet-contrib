// <copyright file="ApplicationInsightsSampler.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.AzureMonitor
{
    /// <summary>
    /// Sample configurable for OpenTelemetry exporters for compatibility
    /// with Application Insight SDKs.
    /// </summary>
    public class ApplicationInsightsSampler : Sampler
    {
        private readonly float samplingRatio;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsSampler"/> class.
        /// </summary>
        /// <param name="samplingRatio">Ratio of telemetry that should be sampled.</param>
        public ApplicationInsightsSampler(float samplingRatio)
        {
            // Ensure passed ratio is between 0 and 1, inclusive
            if (samplingRatio < 0 || samplingRatio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(samplingRatio), "Ratio must be between 0 and 1, inclusive.");
            }

            this.samplingRatio = samplingRatio;
            this.Description = "ApplicationInsightsSampler{" + samplingRatio + "}";
        }

        /// <summary>
        /// Computational method using the DJB2 Hash algorithm to decide whether to sample
        /// a given telemetry item, based on its Trace Id.
        /// </summary>
        /// <param name="samplingParameters">Parameters of telemetry item used to make sampling decision.</param>
        /// <returns>Returns whether or not we should sample telemetry in the form of a <see cref="SamplingResult"/> class.</returns>
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            double sampleScore = DJB2SampleScore(samplingParameters.TraceId.ToHexString().ToLowerInvariant());
            return new SamplingResult(sampleScore < this.samplingRatio);
        }

        private static double DJB2SampleScore(string traceIdHex)
        {
            // Calculate DJB2 hash code from hex-converted TraceId
            int hash = 5381;

            for (int i = 0; i < traceIdHex.Length; i++)
            {
                hash = ((hash << 5) + hash) + (int)traceIdHex[i];
            }

            // Take the absolute value of the hash
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }
            else
            {
                hash = Math.Abs(hash);
            }

            // Divide by MaxValue for value between 0 and 1 for sampling score
            double samplingScore = (double)hash / int.MaxValue;
            return samplingScore;
        }
    }
}

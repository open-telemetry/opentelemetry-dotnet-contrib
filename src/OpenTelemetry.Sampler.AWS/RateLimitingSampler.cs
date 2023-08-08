// <copyright file="RateLimitingSampler.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class RateLimitingSampler : Trace.Sampler
{
    private readonly RateLimiter limiter;

    public RateLimitingSampler(long numPerSecond, Clock clock)
    {
        this.limiter = new RateLimiter(numPerSecond, numPerSecond, clock);
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (this.limiter.TrySpend(1))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        return new SamplingResult(SamplingDecision.Drop);
    }
}

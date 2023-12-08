// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class FallbackSampler : Trace.Sampler
{
    private readonly Trace.Sampler reservoirSampler;
    private readonly Trace.Sampler fixedRateSampler;
    private readonly Clock clock;

    public FallbackSampler(Clock clock)
    {
        this.clock = clock;
        this.reservoirSampler = new ParentBasedSampler(new RateLimitingSampler(1, clock));
        this.fixedRateSampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(0.05));
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        SamplingResult result = this.reservoirSampler.ShouldSample(in samplingParameters);
        if (result.Decision != SamplingDecision.Drop)
        {
            return result;
        }

        return this.fixedRateSampler.ShouldSample(in samplingParameters);
    }
}

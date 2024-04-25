// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using OpenTelemetry.Trace;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Tests;

internal class TestSampler : Sampler
{
    public Func<SamplingParameters, SamplingResult>? SamplingAction { get; set; }

    public SamplingParameters LatestSamplingParameters { get; private set; }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        this.LatestSamplingParameters = samplingParameters;
        return this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(SamplingDecision.RecordAndSample);
    }
}

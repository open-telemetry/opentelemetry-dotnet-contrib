// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Benchmarks;

/// <summary>
/// Benchmarks the per-span cost of <see cref="ConsistentProbabilitySampler.ShouldSample"/> across
/// the different sources it can use to resolve the trace randomness value.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class ConsistentProbabilitySamplerBenchmarks
{
    private readonly Sampler sampler = new ConsistentProbabilitySampler(0.25);

    private SamplingParameters rootSpan;
    private SamplingParameters explicitRandomValue;
    private SamplingParameters randomTraceId;
    private SamplingParameters explicitRandomValueWithOtherMembers;

    [GlobalSetup]
    public void Setup()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        // A root span with no incoming tracestate, so the randomness is generated and recorded.
        this.rootSpan = CreateParameters(default, traceId);

        // A child that inherits an explicit rv value from its parent.
        this.explicitRandomValue = CreateParameters(
            new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "ot=rv:6e6d1a75832a2f"),
            traceId);

        // A child whose parent sets the W3C "random" trace flag (0x2) but no explicit rv, so the
        // randomness is taken from the TraceId.
        // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867
        // will change this code to use ActivityTraceFlags.RandomTraceId.
        this.randomTraceId = CreateParameters(
            new ActivityContext(traceId, spanId, (ActivityTraceFlags)0x3),
            traceId);

        // A child with an explicit rv value alongside other tracestate members that must be preserved.
        this.explicitRandomValueWithOtherMembers = CreateParameters(
            new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "ot=rv:6e6d1a75832a2f,vendorone=abc,vendortwo=def"),
            traceId);
    }

    [Benchmark(Baseline = true)]
    public SamplingResult RootSpan() => this.sampler.ShouldSample(in this.rootSpan);

    [Benchmark]
    public SamplingResult ExplicitRandomValue() => this.sampler.ShouldSample(in this.explicitRandomValue);

    [Benchmark]
    public SamplingResult RandomTraceId() => this.sampler.ShouldSample(in this.randomTraceId);

    [Benchmark]
    public SamplingResult ExplicitRandomValueWithOtherMembers() => this.sampler.ShouldSample(in this.explicitRandomValueWithOtherMembers);

    private static SamplingParameters CreateParameters(ActivityContext parentContext, ActivityTraceId traceId)
        => new(parentContext, traceId, "operation", ActivityKind.Internal, tags: null, links: null);
}

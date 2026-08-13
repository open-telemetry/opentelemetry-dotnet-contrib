// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.Internal;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class ConsistentProbabilitySamplerTests
{
#if NET11_0_OR_GREATER
    private const ActivityTraceFlags RandomTraceIdFlag = ActivityTraceFlags.RandomTraceId;
#else
    private const ActivityTraceFlags RandomTraceIdFlag = (ActivityTraceFlags)0x02;
#endif

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.0000001)]
    [InlineData(2.0)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenProbabilityIsInvalid(double samplingProbability)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConsistentProbabilitySampler(samplingProbability));

        Assert.Equal("samplingProbability", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenProbabilityIsSmallerThanSmallestValidProbability()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConsistentProbabilitySampler(Math.Pow(2, -57)));

        Assert.Equal("samplingProbability", exception.ParamName);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.0001)]
    public void Constructor_AcceptsValidProbability(double samplingProbability)
    {
        var sampler = new ConsistentProbabilitySampler(samplingProbability);

        Assert.NotNull(sampler);
    }

    [Fact]
    public void Constructor_AcceptsSmallestValidProbability()
    {
        var sampler = new ConsistentProbabilitySampler(Math.Pow(2, -56));

        Assert.NotNull(sampler);
    }

    [Theory]
    [InlineData(0.5, "ConsistentProbabilitySampler{0.5}")]
    [InlineData(0.25, "ConsistentProbabilitySampler{0.25}")]
    [InlineData(1.0, "ConsistentProbabilitySampler{1}")]
    public void Description_DescribesTheProbability(double samplingProbability, string expected)
    {
        var sampler = new ConsistentProbabilitySampler(samplingProbability);

        Assert.Equal(expected, sampler.Description);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x80000000000000L)]
    [InlineData(0x00ffffffffffffffL)]
    public void ShouldSample_AlwaysSamplesWhenProbabilityIsOne(long randomness)
    {
        var parameters = CreateRootParameters(randomness);
        var sampler = new ConsistentProbabilitySampler(1.0);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:0", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_SamplesWhenRandomnessEqualsThreshold()
    {
        // At 50% the rejection threshold is exactly 2^55.
        var parameters = CreateRootParameters(0x80000000000000L);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_DropsWhenRandomnessBelowThreshold()
    {
        var parameters = CreateRootParameters(0x7fffffffffffffL);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);

        // The threshold is erased for an unsampled span, and no randomness is added to the context.
        Assert.Equal(string.Empty, result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_UsesExplicitRandomValueInsteadOfTraceId()
    {
        // The TraceID randomness would drop the span (0), but the explicit rv is the maximum value.
        var traceId = CreateTraceId(0L);
        var parent = new ActivityContext(
            traceId,
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: "ot=rv:ffffffffffffff");

        var parameters = CreateParameters(parent, traceId);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8;rv:ffffffffffffff", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_DoesNotUseRandomValueFromMalformedOtEntry()
    {
        // The malformed pair invalidates the ot entry, so the zero-valued TraceID randomness is used
        // instead of the otherwise valid-looking rv value.
        var traceId = CreateTraceId(0L);
        var parent = new ActivityContext(
            traceId,
            ActivitySpanId.CreateRandom(),
            RandomTraceIdFlag,
            traceState: "ot=rv:ffffffffffffff;malformed");

        var parameters = CreateParameters(parent, traceId);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);
        Assert.Equal(string.Empty, result.TraceStateString);
    }

    [Theory]
    [InlineData("ffffffffffffff", SamplingDecision.RecordAndSample)]
    [InlineData("00000000000000", SamplingDecision.Drop)]
    public void ShouldSample_UsesTraceIdWhenRandomFlagIsSet(string trailing, SamplingDecision expected)
    {
        var traceId = ActivityTraceId.CreateFromString((new string('f', 18) + trailing).AsSpan());

        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), RandomTraceIdFlag);
        var parameters = CreateParameters(parent, traceId);

        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(expected, result.Decision);

        // Randomness comes from the TraceID, so no explicit rv is added either way.
        Assert.Equal(expected == SamplingDecision.RecordAndSample ? "ot=th:8" : string.Empty, result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_UsesTraceIdForRootSpanWithoutAddingRandomValue()
    {
        // A root span could legitimately insert an explicit randomness value, but the TraceID travels
        // with every participant in the trace whereas a tracestate entry may be stripped or truncated,
        // so the TraceID is the more robust source of randomness.
        var parameters = CreateRootParameters(0x90000000000000L);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:8", result.TraceStateString);
        Assert.False(OtelTraceState.Parse(result.TraceStateString).HasRandomValue);
    }

    [Fact]
    public void ShouldSample_DoesNotCreateRandomnessForNonRootSpanWithoutRandomFlag()
    {
        // "The Root sampling decision is the only case where it is permitted to modify the explicit
        // trace randomness value for a Context." Two services that receive the same context without
        // an rv value and without the random trace flag must therefore resolve the same randomness,
        // otherwise their decisions for the same trace can disagree.
        var traceId = ActivityTraceId.CreateRandom();
        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var parameters = CreateParameters(parent, traceId);

        var first = new ConsistentProbabilitySampler(0.5).ShouldSample(in parameters);
        var second = new ConsistentProbabilitySampler(0.5).ShouldSample(in parameters);

        Assert.Equal(first.Decision, second.Decision);
        Assert.Equal(first.TraceStateString, second.TraceStateString);

        // The decision is the one implied by the TraceID, and no rv value was invented.
        var expected = GetRandomness(traceId) >= 0x80000000000000L
            ? SamplingDecision.RecordAndSample
            : SamplingDecision.Drop;

        Assert.Equal(expected, first.Decision);
        Assert.False(OtelTraceState.Parse(first.TraceStateString).HasRandomValue);
    }

    [Fact]
    public void ShouldSample_IgnoresUppercaseRandomValue()
    {
        // An rv value must be exactly 14 lower-case hexadecimal digits, so an uppercase value is not
        // valid randomness and the TraceID is used instead.
        var traceId = CreateTraceId(0L);
        var parent = new ActivityContext(
            traceId,
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: "ot=rv:FFFFFFFFFFFFFF");

        var parameters = CreateParameters(parent, traceId);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);
        Assert.Equal(string.Empty, result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_WarnsOnceWhenPresumingTraceIdRandomness()
    {
        // A probability that is unique to this test, so the warning can be attributed to this sampler
        // even if another test writes the same event concurrently.
        const double Probability = 0.123456;

        using var listener = new InMemoryEventListener(OpenTelemetryExtensionsEventSource.Log, EventLevel.Warning);

        var traceId = ActivityTraceId.CreateRandom();
        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var parameters = CreateParameters(parent, traceId);

        var sampler = new ConsistentProbabilitySampler(Probability);

        _ = sampler.ShouldSample(in parameters);
        _ = sampler.ShouldSample(in parameters);

        var warnings = listener.Events.Where(
            p => p.EventId == 5 && p.Payload?.Count == 1 && Equals(p.Payload[0], sampler.Description));

        // "To assist with this migration, the TraceIdRatioBased Sampler issues a warning statement
        // the first time it presumes TraceID randomness for a Context where the Trace random flag is
        // not set." Only the first of the two decisions warns.
        Assert.Single(warnings);
    }

    [Fact]
    public void ShouldSample_DoesNotWarnWhenRandomFlagIsSet()
    {
        const double Probability = 0.234567;

        using var listener = new InMemoryEventListener(OpenTelemetryExtensionsEventSource.Log, EventLevel.Warning);

        var traceId = ActivityTraceId.CreateRandom();
        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), RandomTraceIdFlag);
        var parameters = CreateParameters(parent, traceId);

        var sampler = new ConsistentProbabilitySampler(Probability);

        _ = sampler.ShouldSample(in parameters);

        Assert.DoesNotContain(
            listener.Events,
            p => p.EventId == 5 && p.Payload?.Count == 1 && Equals(p.Payload[0], sampler.Description));
    }

    [Fact]
    public void ShouldSample_DoesNotWarnForRootSpan()
    {
        // A root span has no incoming context whose randomness could be in doubt: the TraceID was
        // generated by this SDK.
        const double Probability = 0.345678;

        using var listener = new InMemoryEventListener(OpenTelemetryExtensionsEventSource.Log, EventLevel.Warning);

        var parameters = CreateRootParameters();
        var sampler = new ConsistentProbabilitySampler(Probability);

        _ = sampler.ShouldSample(in parameters);

        Assert.DoesNotContain(
            listener.Events,
            p => p.EventId == 5 && p.Payload?.Count == 1 && Equals(p.Payload[0], sampler.Description));
    }

    [Fact]
    public void ShouldSample_WarnsOnceWhenThresholdDoesNotFitInTraceState()
    {
        const double Probability = 0.456789;

        using var listener = new InMemoryEventListener(OpenTelemetryExtensionsEventSource.Log, EventLevel.Warning);

        var value = new string('a', OtelTraceState.TraceStateSizeLimit - "foo:".Length);
        var traceState = $"ot=foo:{value}";
        var traceId = CreateTraceId(ConsistentProbability.MaxRandomValue);
        var parent = new ActivityContext(
            traceId,
            ActivitySpanId.CreateRandom(),
            RandomTraceIdFlag,
            traceState: traceState);
        var parameters = CreateParameters(parent, traceId);
        var sampler = new ConsistentProbabilitySampler(Probability);

        var first = sampler.ShouldSample(in parameters);
        var second = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, first.Decision);
        Assert.Equal(traceState, first.TraceStateString);
        Assert.Equal(traceState, second.TraceStateString);

        var warnings = listener.Events.Where(
            p => p.EventId == 6 && p.Payload?.Count == 1 && Equals(p.Payload[0], sampler.Description));

        Assert.Single(warnings);
    }

    [Fact]
    public void ShouldSample_PreservesOtherTraceStateMembers()
    {
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: "ot=rv:ffffffffffffff,vendor=abc");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal("ot=th:8;rv:ffffffffffffff,vendor=abc", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_IgnoresParentThresholdAndEncodesItsOwn()
    {
        // "A consistent probability sampling decision ignores the parent's sampling threshold (if
        // any)." The parent was sampled at 50% (th:8), but this sampler applies its own 25% (th:c).
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: "ot=th:8;rv:ffffffffffffff");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.25);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);

        // The outgoing threshold is this sampler's (th:c), not the parent's (th:8).
        Assert.Equal("ot=th:c;rv:ffffffffffffff", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_DropsIndependentlyOfParentThreshold()
    {
        // The parent was sampled at 100% (th:0), but an independent decision based on the shared
        // randomness (R = 0) drops the span at 50%, rather than inheriting the parent's decision.
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: "ot=th:0;rv:00000000000000");

        var parameters = CreateParameters(parent);
        var sampler = new ConsistentProbabilitySampler(0.5);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.Drop, result.Decision);

        // The parent's threshold is erased because this span is not sampled here.
        Assert.Equal("ot=rv:00000000000000", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_IsConsistentAcrossProbabilities()
    {
        // A span kept at probability p1 must also be kept at any probability p2 >= p1, given the
        // same randomness value.
        const long Randomness = 0x90000000000000L; // ~56.25% into the range.
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            traceState: FormattableString.Invariant($"ot=rv:{Randomness:x14}"));

        var parameters = CreateParameters(parent);

        Assert.Equal(SamplingDecision.RecordAndSample, Sample(0.5));
        Assert.Equal(SamplingDecision.RecordAndSample, Sample(0.75));
        Assert.Equal(SamplingDecision.RecordAndSample, Sample(1.0));

        // A much lower probability drops it.
        Assert.Equal(SamplingDecision.Drop, Sample(0.1));

        SamplingDecision Sample(double probability)
        {
            return new ConsistentProbabilitySampler(probability).ShouldSample(in parameters).Decision;
        }
    }

    [Fact]
    public void ShouldSample_ApproximatesConfiguredProbabilityAcrossRandomTraceIds()
    {
        const int Iterations = 100_000;
        const double Probability = 0.25;

        var sampler = new ConsistentProbabilitySampler(Probability);

        var sampled = 0;
        for (var i = 0; i < Iterations; i++)
        {
            // Each iteration is a new root span, whose randomness is the trailing 56 bits of a newly
            // generated (random) TraceID.
            var parameters = CreateRootParameters();

            if (sampler.ShouldSample(in parameters).Decision == SamplingDecision.RecordAndSample)
            {
                sampled++;
            }
        }

        var fraction = (double)sampled / Iterations;

        Assert.InRange(fraction, Probability - 0.01, Probability + 0.01);
    }

    [Fact]
    public void ShouldSample_PropagatesRandomnessConsistentlyAcrossProcesses()
    {
        const double Probability = 0.5;

        var producerSource = nameof(this.ShouldSample_PropagatesRandomnessConsistentlyAcrossProcesses) + ".Producer";
        var carrier = new Dictionary<string, string>();
        ActivityContext rootContext;
        long randomness;
        bool expectedSampled;
        string expectedTraceState;

        // Process 1: a service starts a root span at 50%.
        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddSource(producerSource)
                                 .SetSampler(new ConsistentProbabilitySampler(Probability))
                                 .Build())
        using (var source = new ActivitySource(producerSource))
        using (var root = source.StartActivity("root", ActivityKind.Server))
        {
            Assert.NotNull(root);

            // The randomness of the whole trace is the trailing 56 bits of the TraceID the SDK
            // generated for the root span, so the expected decision follows from it.
            randomness = GetRandomness(root.TraceId);
            expectedSampled = IsSampled(Probability, randomness);
            expectedTraceState = expectedSampled ? "ot=th:8" : string.Empty;

            // The sampler encoded its threshold, and left the randomness to the TraceID rather than
            // adding an rv sub-key. The tracestate, rather than Activity.Recorded, is what reflects
            // this sampler's decision: any other ActivityListener in the process can record the
            // Activity as well.
            Assert.Equal(expectedTraceState, root.TraceStateString ?? string.Empty);

            if (expectedSampled)
            {
                Assert.True(root.Recorded, "The root span was not recorded.");
            }

            rootContext = root.Context;

            // Serialize the span context into W3C traceparent/tracestate headers, as when sending a
            // request to another service.
            var outwardPropagator = new TraceContextPropagator();
            outwardPropagator.Inject(
                new(root.Context, Baggage.Current),
                carrier,
                static (headers, key, value) => headers[key] = value);
        }

        // The randomness travelled on the wire in the traceparent header.
        Assert.Equal(rootContext.TraceId.ToHexString(), carrier["traceparent"].Split('-')[1]);

        if (expectedSampled)
        {
            Assert.Equal(expectedTraceState, carrier["tracestate"]);
        }
        else
        {
            Assert.False(carrier.ContainsKey("tracestate"), "An unsampled span should not emit a tracestate.");
        }

        // The wire boundary: a different process extracts the propagated context.
        var inwardPropagator = new TraceContextPropagator();
        var context = inwardPropagator.Extract(
            default,
            carrier,
            static (headers, key) => headers.TryGetValue(key, out var value) ? [value] : []);

        var remoteParent = context.ActivityContext;

        Assert.True(remoteParent.IsRemote, "The extracted context is not marked as remote.");
        Assert.Equal(rootContext.TraceId, remoteParent.TraceId);
        Assert.Equal(rootContext.SpanId, remoteParent.SpanId);

        // Process 2: a downstream service continues the trace from the received context. A real child
        // span is created across the process boundary and joins the same trace.
        var consumerSource = nameof(this.ShouldSample_PropagatesRandomnessConsistentlyAcrossProcesses) + ".Consumer";

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddSource(consumerSource)
                                 .SetSampler(new ConsistentProbabilitySampler(Probability))
                                 .Build())
        using (var source = new ActivitySource(consumerSource))
        using (var child = source.StartActivity("child", ActivityKind.Server, remoteParent))
        {
            Assert.NotNull(child);
            Assert.True(child.HasRemoteParent, "The child span does not have a remote parent.");
            Assert.Equal(rootContext.TraceId, child.TraceId);
            Assert.Equal(rootContext.SpanId, child.ParentSpanId);

            // At the same probability the child decides consistently with the root, using the
            // randomness that travelled in the TraceID.
            Assert.Equal(expectedTraceState, child.TraceStateString ?? string.Empty);

            if (expectedSampled)
            {
                Assert.True(child.Recorded, "The child span was not recorded.");
            }
        }

        // The sampling decision made from the received context is driven by the propagated TraceID,
        // and does not add randomness of its own.
        var remoteParameters = new SamplingParameters(
            remoteParent,
            remoteParent.TraceId,
            "child",
            ActivityKind.Server);

        var sampler = new ConsistentProbabilitySampler(Probability);
        var remoteResult = sampler.ShouldSample(remoteParameters);

        Assert.Equal(expectedSampled ? SamplingDecision.RecordAndSample : SamplingDecision.Drop, remoteResult.Decision);
        Assert.Equal(expectedTraceState, remoteResult.TraceStateString);

        // Consistency: kept at p1 implies kept at any p2 >= p1, while a lower probability
        // that excludes this randomness consistently drops it.
        Assert.Equal(ExpectedDecision(0.75), RemoteDecision(0.75));
        Assert.Equal(ExpectedDecision(0.25), RemoteDecision(0.25));

        SamplingDecision ExpectedDecision(double probability)
        {
            return IsSampled(probability, randomness) ? SamplingDecision.RecordAndSample : SamplingDecision.Drop;
        }

        SamplingDecision RemoteDecision(double probability)
        {
            return new ConsistentProbabilitySampler(probability).ShouldSample(remoteParameters).Decision;
        }
    }

    [Fact]
    public void ShouldSample_EncodesProbabilitiesNearOneExactly()
    {
        // 1 - 2^-8 = 0.99609375. The frexp(1 - probability) precision boost encodes this exactly as
        // th:01 even at the default precision, where the reference float method would be coarse.
        var parameters = CreateRootParameters(ConsistentProbability.MaxRandomValue);
        var sampler = new ConsistentProbabilitySampler(1.0 - (1.0 / 256.0));

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:01", result.TraceStateString);
    }

    [Fact]
    public void ShouldSample_UsesTrailingBytesOfTraceIdForRandomness()
    {
        // The leading 18 hex digits of the TraceID are ignored; only the trailing 14 (56 bits) are
        // the randomness value, here 0xd29d6a7215ced0.
        // https://github.com/open-telemetry/opentelemetry-collector-contrib/blob/6d20534d0a232acaa8cf7161ddbaeab6915e0c01/pkg/sampling/threshold_test.go#L64-L87
        var traceId = ActivityTraceId.CreateFromString("abababababababababd29d6a7215ced0".AsSpan());

        var parent = new ActivityContext(traceId, ActivitySpanId.CreateRandom(), RandomTraceIdFlag);
        var parameters = CreateParameters(parent, traceId);

        // 25% sampling has threshold "c" (0xc0000000000000); 0xd29d6a7215ced0 >= it, so sampled.
        var sampler = new ConsistentProbabilitySampler(0.25);

        var result = sampler.ShouldSample(in parameters);

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
        Assert.Equal("ot=th:c", result.TraceStateString);
    }

    private static bool IsSampled(double probability, long randomness)
    {
        var threshold = ConsistentProbability.DecodeThreshold(
            ConsistentProbability.EncodeThreshold(probability, ConsistentProbability.DefaultPrecision));

        return randomness >= threshold;
    }

    private static long GetRandomness(ActivityTraceId traceId)
    {
        var hex = traceId.ToHexString();

        Assert.True(ConsistentProbability.TryParseHex56(hex.AsSpan(hex.Length - ConsistentProbability.MaxHexDigits), out var value));

        return value;
    }

    private static ActivityTraceId CreateTraceId(long randomness)
    {
        var hex = new string('a', 32 - ConsistentProbability.MaxHexDigits) +
                  randomness.ToString("x14", CultureInfo.InvariantCulture);

        return ActivityTraceId.CreateFromString(hex.AsSpan());
    }

    private static SamplingParameters CreateRootParameters()
        => CreateParameters(default, ActivityTraceId.CreateRandom());

    private static SamplingParameters CreateRootParameters(long randomness)
        => CreateParameters(default, CreateTraceId(randomness));

    private static SamplingParameters CreateParameters(ActivityContext parentContext)
        => CreateParameters(parentContext, ActivityTraceId.CreateRandom());

    private static SamplingParameters CreateParameters(ActivityContext parentContext, ActivityTraceId traceId)
        => new(parentContext, traceId, "TestOperation", ActivityKind.Internal, tags: null, links: null);
}

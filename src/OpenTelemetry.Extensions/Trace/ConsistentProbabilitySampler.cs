// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Extensions.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// A <see cref="Sampler"/> that makes consistent probability based sampling decisions following the
/// OpenTelemetry
/// <see href="https://opentelemetry.io/docs/specs/otel/trace/tracestate-probability-sampling/">
/// probability sampling</see> specification.
/// </summary>
/// <remarks>
/// Because all participants in a trace share the same source of randomness, their sampling decisions
/// are consistent with one another. Like the built-in <c>TraceIdRatioBased</c> sampler, this sampler
/// makes an independent decision, so combine it with a parent based sampler to follow the parent's
/// decision for non-root spans.
/// </remarks>
public sealed class ConsistentProbabilitySampler : Sampler
{
#if !NET
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
#endif

    private readonly Random? random;
    private readonly long threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsistentProbabilitySampler"/> class.
    /// </summary>
    /// <param name="samplingProbability">
    /// The probability with which spans are sampled, in the range <c>[2^-56, 1]</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="samplingProbability"/> is not a number, or is outside the range <c>[2^-56, 1]</c>.
    /// </exception>
    public ConsistentProbabilitySampler(double samplingProbability)
        : this(samplingProbability, null)
    {
    }

    internal ConsistentProbabilitySampler(double samplingProbability, Random? random)
    {
        // The smallest probability representable by the 56-bit randomness range used by the
        // specification is 2^-56 (i.e. an adjusted count of 2^56).
        const double MinProbability = 1.0 / ConsistentProbability.MaxAdjustedCount;

        if (double.IsNaN(samplingProbability) || samplingProbability < MinProbability || samplingProbability > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(samplingProbability),
                samplingProbability,
                "Value must be in the range [2^-56, 1].");
        }

        this.random = random;

        // Round the probability to the encoded threshold once, so the sampling decision matches the
        // threshold that is propagated to downstream participants.
        var encoded = ConsistentProbability.EncodeThreshold(samplingProbability, ConsistentProbability.DefaultPrecision);
        this.threshold = ConsistentProbability.DecodeThreshold(encoded);

        this.Description = FormattableString.Invariant($"ConsistentProbabilitySampler{{{samplingProbability}}}");
    }

    private static Random DefaultRandom =>
#if NET
        Random.Shared;
#else
        ThreadLocalRandom.Value!;
#endif

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var parentContext = samplingParameters.ParentContext;
        var traceState = OtelTraceState.Parse(parentContext.TraceState);

        // "A common random value (that is known or propagated to all participants) is the main
        // ingredient that enables consistent probability sampling." The specification supports two
        // sources: an explicit rv value, or the least-significant 56 bits of the TraceID.
        long randomness;
        if (traceState.HasRandomValue)
        {
            // Prefer the explicit randomness value. "Explicit randomness values are meant to
            // propagate through span contexts unmodified."
            randomness = traceState.RandomValue;
        }
        else if ((parentContext.TraceFlags & (ActivityTraceFlags)2) != 0)
        {
            // TODO: Use ActivityTraceFlags.RandomTraceId above once available.
            // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867

            // "Using the least-significant 56 bits of the TraceID as the source of randomness [...]
            // can be done if the root Span's Trace SDK knows that the TraceID has been generated in
            // a random or pseudo-random manner", which the random trace flag indicates.
            randomness = GetRandomnessFromTraceId(samplingParameters.TraceId);
        }
        else
        {
            // No usable source of randomness is available, so generate one. "The Root sampling
            // decision is the only case where it is permitted to modify the explicit trace
            // randomness value for a Context." Record it so the rest of the trace stays consistent.
            randomness = this.GenerateRandomness();
            traceState.SetRandomValue(randomness);
        }

        // "If R >= T, keep the span, else drop the span."
        var sampled = randomness >= this.threshold;

        if (sampled)
        {
            // "When a Span or Context is sampled, the sampler's effective T is encoded in the
            // OpenTelemetry TraceState th sub-key to indicate its sampling probability."
            traceState.SetThreshold(this.threshold);
        }
        else
        {
            // "Sampling stages that yield spans with unknown sampling probability [...] must erase
            // the OpenTelemetry threshold value in their output."
            traceState.ClearThreshold();
        }

        return new(
            sampled ? SamplingDecision.RecordAndSample : SamplingDecision.Drop,
            traceState.Serialize());
    }

    private static long GetRandomnessFromTraceId(ActivityTraceId traceId)
    {
        // The randomness is the trailing 7 bytes (56 bits) of the 16-byte (32 hexadecimal digit) TraceId.
        var hex = traceId.ToHexString();
        _ = ConsistentProbability.TryParseHex56(hex.AsSpan(hex.Length - ConsistentProbability.MaxHexDigits), out var value);
        return value;
    }

    [SuppressMessage(
        "Security",
        "CA5394",
        Justification = "A cryptographically secure random number generator is not required for probability sampling.")]
    private long GenerateRandomness()
    {
        // "OpenTelemetry supports a random (or pseudo-random) 56-bit value known as explicit trace randomness."
        var random = this.random ?? DefaultRandom;

#if NET
        // The randomness is a 56-bit value, i.e. in the range [0, 2^56).
        return random.NextInt64(0, ConsistentProbability.MaxAdjustedCount);
#else
        // Assemble a 56-bit value from the trailing 7 bytes produced by the generator.
        var buffer = new byte[7];
        random.NextBytes(buffer);

        long value = 0;

        foreach (var b in buffer)
        {
            value = (value << 8) | b;
        }

        return value;
#endif
    }
}

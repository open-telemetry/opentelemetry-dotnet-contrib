// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
    // The W3C Trace Context Level 2 "random" trace flag, which indicates that the least-significant
    // 56 bits of the TraceID were generated in a random or pseudo-random manner.
    // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867
    // will change this code to use ActivityTraceFlags.RandomTraceId.
    private const ActivityTraceFlags RandomTraceIdFlag = (ActivityTraceFlags)0x02;

    private readonly long threshold;

    private int hasWarnedAboutPresumedRandomness;

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

        // Round the probability to the encoded threshold once, so the sampling decision matches the
        // threshold that is propagated to downstream participants.
        var encoded = ConsistentProbability.EncodeThreshold(samplingProbability, ConsistentProbability.DefaultPrecision);
        this.threshold = ConsistentProbability.DecodeThreshold(encoded);

        this.Description = FormattableString.Invariant($"ConsistentProbabilitySampler{{{samplingProbability}}}");
    }

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
            // propagate through span contexts unmodified", and "SDKs and Samplers MUST NOT
            // overwrite explicit randomness in an OpenTelemetry TraceState value".
            randomness = traceState.RandomValue;
        }
        else
        {
            // "Samplers SHOULD presume that TraceIDs meet the W3C Trace Context Level 2 randomness
            // requirements, unless an explicit randomness value is present in the rv sub-key."
            //
            // Deriving the randomness from the TraceID, rather than generating a new value, is what
            // keeps this decision consistent with every other participant that observes the same
            // TraceID. Generating one here would be permitted for a root Context only ("The Root
            // sampling decision is the only case where it is permitted to modify the explicit trace
            // randomness value for a Context"), but a generated value only reaches other
            // participants through the tracestate header, whereas the TraceID always travels with
            // the trace.
            if (parentContext.IsValid() && (parentContext.TraceFlags & RandomTraceIdFlag) == 0)
            {
                // "To assist with this migration, the TraceIdRatioBased Sampler issues a warning
                // statement the first time it presumes TraceID randomness for a Context where the
                // Trace random flag is not set."
                this.WarnOncePresumingTraceIdRandomness();
            }

            randomness = GetRandomnessFromTraceId(samplingParameters.TraceId);
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

    private void WarnOncePresumingTraceIdRandomness()
    {
        // The relaxed read keeps the common case (already warned) off the interlocked path, as this
        // runs for every span that does not carry the random trace flag.
        if (Volatile.Read(ref this.hasWarnedAboutPresumedRandomness) == 0 &&
            Interlocked.Exchange(ref this.hasWarnedAboutPresumedRandomness, 1) == 0)
        {
            OpenTelemetryExtensionsEventSource.Log.PresumedTraceIdRandomness(this.Description);
        }
    }
}

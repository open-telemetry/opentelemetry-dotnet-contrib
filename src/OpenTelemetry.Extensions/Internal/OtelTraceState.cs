// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace OpenTelemetry.Extensions.Internal;

/// <summary>
/// Parses and serializes the OpenTelemetry <c>ot</c> entry of a W3C <c>tracestate</c>, exposing the
/// <c>th</c> (rejection threshold) and <c>rv</c> (explicit randomness value) sub-keys used for
/// consistent probability sampling, while preserving any other <c>ot</c> sub-keys and unrelated
/// <c>tracestate</c> members.
/// </summary>
internal struct OtelTraceState
{
    /// <summary>
    /// The W3C <c>tracestate</c> key that holds OpenTelemetry values.
    /// </summary>
    public const string TraceStateKey = "ot";

    /// <summary>
    /// The <c>ot</c> sub-key holding the rejection threshold.
    /// </summary>
    public const string ThresholdSubKey = "th";

    /// <summary>
    /// The <c>ot</c> sub-key holding the explicit randomness value.
    /// </summary>
    public const string RandomValueSubKey = "rv";

    /// <summary>
    /// The maximum length of the serialized <c>ot</c> value, per the specification.
    /// </summary>
    public const int TraceStateSizeLimit = 256;

    private List<KeyValuePair<string, string>>? otherSubKeys;
    private List<string>? otherMembers;

    /// <summary>
    /// Gets the rejection threshold.
    /// Only meaningful when <see cref="HasThreshold"/> is <see langword="true"/>.
    /// </summary>
    public long Threshold { get; private set; }

    /// <summary>
    /// Gets the explicit randomness value.
    /// Only meaningful when <see cref="HasRandomValue"/> is <see langword="true"/>.
    /// </summary>
    public long RandomValue { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a valid threshold is present.
    /// </summary>
    public bool HasThreshold { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a valid randomness value is present.
    /// </summary>
    public bool HasRandomValue { get; private set; }

    /// <summary>Parses a W3C <c>tracestate</c> string.</summary>
    /// <param name="traceState">The <c>tracestate</c> value, which may be <see langword="null"/> or empty.</param>
    /// <returns>The parsed <see cref="OtelTraceState"/>.</returns>
    public static OtelTraceState Parse(string? traceState)
    {
        var state = default(OtelTraceState);

        if (string.IsNullOrEmpty(traceState))
        {
            return state;
        }

        var remaining = traceState.AsSpan();
        while (!remaining.IsEmpty)
        {
            var comma = remaining.IndexOf(',');
            var member = (comma < 0 ? remaining : remaining.Slice(0, comma)).Trim();
            remaining = comma < 0 ? default : remaining.Slice(comma + 1);

            if (member.IsEmpty)
            {
                continue;
            }

            var separator = member.IndexOf('=');
            if (separator <= 0)
            {
                // Malformed member: preserve it verbatim rather than discarding data.
                state.otherMembers ??= [];
                state.otherMembers.Add(member.ToString());
                continue;
            }

            if (member.Slice(0, separator).Equals(TraceStateKey, StringComparison.Ordinal))
            {
                state.ParseOtValue(member.Slice(separator + 1));
            }
            else
            {
                state.otherMembers ??= [];
                state.otherMembers.Add(member.ToString());
            }
        }

        return state;
    }

    /// <summary>
    /// Sets the rejection threshold.
    /// </summary>
    /// <param name="threshold">The rejection threshold, in the range <c>[0, 2^56)</c>.</param>
    public void SetThreshold(long threshold)
    {
        this.Threshold = threshold;
        this.HasThreshold = true;
    }

    /// <summary>
    /// Removes the rejection threshold, marking the sampling probability as unknown.
    /// </summary>
    public void ClearThreshold()
    {
        this.Threshold = 0;
        this.HasThreshold = false;
    }

    /// <summary>
    /// Sets the explicit randomness value.
    /// </summary>
    /// <param name="randomValue">The randomness value, in the range <c>[0, 2^56)</c>.</param>
    public void SetRandomValue(long randomValue)
    {
        this.RandomValue = randomValue;
        this.HasRandomValue = true;
    }

    /// <summary>
    /// Serializes the state back into a W3C <c>tracestate</c> string.
    /// </summary>
    /// <returns>
    /// The serialized <c>tracestate</c>, or an empty string when there is nothing to emit.
    /// </returns>
    public readonly string Serialize()
    {
        var hasOtContent = this.HasThreshold || this.HasRandomValue || this.otherSubKeys is { Count: > 0 };

        if (!hasOtContent && this.otherMembers is not { Count: > 0 })
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        if (hasOtContent)
        {
            this.AppendOtEntry(builder);
        }

        if (this.otherMembers is { Count: > 0 } other)
        {
            foreach (var member in other)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(member);
            }
        }

        return builder.ToString();
    }

    private static void AppendSubKey(StringBuilder builder, int valueIndex, string name, string value)
    {
        if (builder.Length > valueIndex)
        {
            builder.Append(';');
        }

        builder.Append(name)
               .Append(':')
               .Append(value);
    }

    private static void AppendHex14(StringBuilder builder, long value)
    {
        const string Format = "x14";

#if NET
        Span<char> buffer = stackalloc char[ConsistentProbability.MaxHexDigits];
        _ = value.TryFormat(buffer, out var written, Format, CultureInfo.InvariantCulture);
        builder.Append(buffer.Slice(0, written));
#else
        builder.Append(value.ToString(Format, CultureInfo.InvariantCulture));
#endif
    }

    private void ParseOtValue(ReadOnlySpan<char> otValue)
    {
        while (!otValue.IsEmpty)
        {
            var semicolon = otValue.IndexOf(';');
            var pair = (semicolon < 0 ? otValue : otValue.Slice(0, semicolon)).Trim();
            otValue = semicolon < 0 ? default : otValue.Slice(semicolon + 1);

            if (pair.IsEmpty)
            {
                continue;
            }

            var separator = pair.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = pair.Slice(0, separator);
            var value = pair.Slice(separator + 1);

            if (name.Equals(ThresholdSubKey, StringComparison.Ordinal))
            {
                // A th value has 1 to 14 hexadecimal digits; it is extended with trailing zeros to
                // 14 digits when decoded. An invalid value leaves the threshold erased.
                if (ConsistentProbability.TryDecodeThreshold(value, out var parsed))
                {
                    this.Threshold = parsed;
                    this.HasThreshold = true;
                }
            }
            else if (name.Equals(RandomValueSubKey, StringComparison.Ordinal))
            {
                // An rv value must be exactly 14 hexadecimal digits.
                if (value.Length == ConsistentProbability.MaxHexDigits &&
                    ConsistentProbability.TryParseHex56(value, out var parsed))
                {
                    this.RandomValue = parsed;
                    this.HasRandomValue = true;
                }
            }
            else
            {
                this.otherSubKeys ??= [];
                this.otherSubKeys.Add(new(name.ToString(), value.ToString()));
            }
        }
    }

    private readonly void AppendOtEntry(StringBuilder builder)
    {
        var prefixIndex = builder.Length;

        builder.Append(TraceStateKey)
               .Append('=');

        var valueIndex = builder.Length;

        if (this.HasThreshold)
        {
            AppendSubKey(
                builder,
                valueIndex,
                ThresholdSubKey,
                ConsistentProbability.EncodeThresholdInteger(this.Threshold));
        }

        if (this.HasRandomValue)
        {
            if (builder.Length > valueIndex)
            {
                builder.Append(';');
            }

            builder.Append(RandomValueSubKey)
                   .Append(':');

            AppendHex14(builder, this.RandomValue);
        }

        if (this.otherSubKeys is { Count: > 0 } other)
        {
            foreach (var subKey in other)
            {
                // Preserve additional sub-keys only while the ot value stays within the size limit.
                var otValueLength = builder.Length - valueIndex;
                var addedLength = (otValueLength > 0 ? 1 : 0) + subKey.Key.Length + 1 + subKey.Value.Length;

                if (otValueLength + addedLength > TraceStateSizeLimit)
                {
                    continue;
                }

                AppendSubKey(builder, valueIndex, subKey.Key, subKey.Value);
            }
        }

        if (builder.Length == valueIndex)
        {
            // Only oversized sub-keys were present, so remove the empty "ot=" prefix.
            builder.Length = prefixIndex;
        }
    }
}

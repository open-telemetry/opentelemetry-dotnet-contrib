// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net;
#endif
using System.Diagnostics;
using System.Globalization;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Extensions.AWS.Trace;

/// <summary>
/// Propagator for AWS X-Ray. See https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-tracingheader.
/// </summary>
public class AWSXRayPropagator : TextMapPropagator
{
    private const string AWSXRayTraceHeaderKey = "X-Amzn-Trace-Id";
    private const char KeyValueDelimiter = '=';
    private const char TraceHeaderDelimiter = ';';

    private const string RootKey = "Root";
    private const char Version = '1';
    private const int RandomNumberHexDigits = 24;
    private const int EpochHexDigits = 8;
    private const int TotalLength = 35;
    private const char TraceIdDelimiter = '-';
    private const int TraceIdDelimiterFirstIndex = 1;
    private const int TraceIdDelimiterSecondIndex = 10;

    private const string ParentKey = "Parent";
    private const int ParentIdHexDigits = 16;

    private const string SampledKey = "Sampled";
    private const char SampledValue = '1';
    private const char NotSampledValue = '0';

    // The length of a header in the format "Root=1-{8 hex}-{24 hex};Parent={16 hex};Sampled={0|1}".
    private const int TraceHeaderLength = 74;

    private static readonly HashSet<string> AllFields = [AWSXRayTraceHeaderKey];

    /// <inheritdoc/>
    /// <remarks>
    /// Callers should not modify the returned set.
    /// </remarks>
    public override ISet<string> Fields => AllFields;

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        if (context.ActivityContext.IsValid())
        {
            return context;
        }

        if (carrier == null)
        {
            AWSXRayEventSource.Log.FailedToExtractActivityContext(nameof(AWSXRayPropagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            AWSXRayEventSource.Log.FailedToExtractActivityContext(nameof(AWSXRayPropagator), "null getter");
            return context;
        }

        try
        {
            var parentTraceHeader = getter(carrier, AWSXRayTraceHeaderKey);

            if (parentTraceHeader == null || parentTraceHeader.Count() != 1)
            {
                return context;
            }

            var parentHeader = parentTraceHeader.First();

            return !TryParseXRayTraceHeader(parentHeader, out var newActivityContext) ? context : new PropagationContext(newActivityContext, context.Baggage);
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ActivityContextExtractException(nameof(AWSXRayPropagator), ex);
        }

        return context;
    }

    /// <inheritdoc/>
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
        {
            AWSXRayEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "Invalid context");
            return;
        }

        if (carrier == null)
        {
            AWSXRayEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            AWSXRayEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "null setter");
            return;
        }

#if !NETFRAMEWORK
        if (carrier.GetType() == typeof(HttpRequestMessage))
        {
            var httpRequestMessage = (HttpRequestMessage)(object)carrier;

            // If X-Amzn-Trace-Id already exists in the headers and the carrier is of HttpRequestMessage,
            // This means that the request is coming from the AWS SDK Instrumentation library and in this
            // case, we don't want to overwrite the propagation context from the AWS SDK Span with the
            // context from the outgoing HttpRequest
            if (httpRequestMessage.Headers.Contains(AWSXRayTraceHeaderKey))
            {
                return;
            }
        }
#endif

#if NETFRAMEWORK
        if (carrier.GetType() == typeof(HttpWebRequest))
        {
            var httpWebRequest = (HttpWebRequest)(object)carrier;

            if (httpWebRequest.Headers.Get(AWSXRayTraceHeaderKey) != null)
            {
                return;
            }
        }
#endif

        // The header always has the same length, so build it in a buffer on the stack.
        var traceId = context.ActivityContext.TraceId.ToHexString().AsSpan();
        Span<char> header = stackalloc char[TraceHeaderLength];
        var length = 0;

        Append(header, ref length, RootKey.AsSpan());
        header[length++] = KeyValueDelimiter;
        header[length++] = Version;
        header[length++] = TraceIdDelimiter;
        Append(header, ref length, traceId.Slice(0, EpochHexDigits));
        header[length++] = TraceIdDelimiter;
        Append(header, ref length, traceId.Slice(EpochHexDigits));
        header[length++] = TraceHeaderDelimiter;
        Append(header, ref length, ParentKey.AsSpan());
        header[length++] = KeyValueDelimiter;
        Append(header, ref length, context.ActivityContext.SpanId.ToHexString().AsSpan());
        header[length++] = TraceHeaderDelimiter;
        Append(header, ref length, SampledKey.AsSpan());
        header[length++] = KeyValueDelimiter;
        header[length++] = (context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? SampledValue : NotSampledValue;

        setter(carrier, AWSXRayTraceHeaderKey, header.Slice(0, length).ToString());

        static void Append(Span<char> destination, ref int position, ReadOnlySpan<char> value)
        {
            value.CopyTo(destination.Slice(position));
            position += value.Length;
        }
    }

    internal static bool TryParseXRayTraceHeader(string rawHeader, out ActivityContext activityContext)
    {
        // from https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-tracingheader
        // rawHeader format: Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1

        activityContext = default;
        Span<char> traceIdBuffer = stackalloc char[EpochHexDigits + RandomNumberHexDigits];
        scoped ReadOnlySpan<char> traceId = default;
        ReadOnlySpan<char> parentId = default;
        char traceOptions = default;

        if (string.IsNullOrEmpty(rawHeader))
        {
            return false;
        }

        var header = rawHeader.AsSpan();
        while (header.Length > 0)
        {
            var delimiterIndex = header.IndexOf(TraceHeaderDelimiter);
            ReadOnlySpan<char> part;
            if (delimiterIndex >= 0)
            {
                part = header.Slice(0, delimiterIndex);
                header = header.Slice(delimiterIndex + 1);
            }
            else
            {
                part = header.Slice(0);
                header = header.Slice(header.Length);
            }

            var trimmedPart = part.Trim();
            var equalsIndex = trimmedPart.IndexOf(KeyValueDelimiter);
            if (equalsIndex < 0)
            {
                return false;
            }

            var value = trimmedPart.Slice(equalsIndex + 1);
            if (trimmedPart.StartsWith(RootKey.AsSpan()))
            {
                if (!TryParseOTFormatTraceId(value, traceIdBuffer))
                {
                    return false;
                }

                traceId = traceIdBuffer;
            }
            else if (trimmedPart.StartsWith(ParentKey.AsSpan()))
            {
                if (!IsParentIdValid(value))
                {
                    return false;
                }

                parentId = value;
            }
            else if (trimmedPart.StartsWith(SampledKey.AsSpan()))
            {
                if (!TryParseSampleDecision(value, out var sampleDecision))
                {
                    return false;
                }

                traceOptions = sampleDecision;
            }
        }

        if (traceId.IsEmpty || parentId.IsEmpty || traceOptions == default)
        {
            return false;
        }

        var activityTraceId = ActivityTraceId.CreateFromString(traceId);
        var activityParentId = ActivitySpanId.CreateFromString(parentId);
        var activityTraceOptions = traceOptions == SampledValue ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        activityContext = new ActivityContext(activityTraceId, activityParentId, activityTraceOptions, isRemote: true);

        return true;
    }

    internal static bool TryParseOTFormatTraceId(ReadOnlySpan<char> traceId, Span<char> otFormatTraceId)
    {
        if (traceId.IsEmpty || traceId.IsWhiteSpace())
        {
            return false;
        }

        if (traceId.Length != TotalLength)
        {
            return false;
        }

        if (traceId.Length < 1 || traceId[0] != Version)
        {
            return false;
        }

        if (traceId[TraceIdDelimiterFirstIndex] != TraceIdDelimiter || traceId[TraceIdDelimiterSecondIndex] != TraceIdDelimiter)
        {
            return false;
        }

        var timestamp = traceId.Slice(TraceIdDelimiterFirstIndex + 1, EpochHexDigits);
        var randomNumber = traceId.Slice(TraceIdDelimiterSecondIndex + 1);
        if (timestamp.Length != EpochHexDigits || randomNumber.Length != RandomNumberHexDigits)
        {
            return false;
        }

        if (!IsHexNumber(timestamp) || !IsHexNumber(randomNumber))
        {
            return false;
        }

        timestamp.CopyTo(otFormatTraceId);
        randomNumber.CopyTo(otFormatTraceId.Slice(EpochHexDigits));

        return true;
    }

    internal static bool IsParentIdValid(ReadOnlySpan<char> parentId) =>
        !parentId.IsEmpty &&
        !parentId.IsWhiteSpace() &&
        parentId.Length == ParentIdHexDigits &&
        IsHexNumber(parentId);

    internal static bool TryParseSampleDecision(ReadOnlySpan<char> sampleDecision, out char result)
    {
        result = default;

        if (sampleDecision.IsEmpty || sampleDecision.IsWhiteSpace())
        {
            return false;
        }

        if (sampleDecision.Length != 1)
        {
            return false;
        }

        var tempChar = sampleDecision[0];

        if (tempChar is not SampledValue and not NotSampledValue)
        {
            return false;
        }

        result = tempChar;

        return true;
    }

    /// <summary>
    /// Determines whether the value is a hexadecimal number in the same way as parsing it with
    /// <see cref="NumberStyles.HexNumber"/> (as previously done with <c>int.TryParse()</c>,
    /// <c>long.TryParse()</c> and <c>BigInteger.TryParse()</c>), but without allocating. That is, one or
    /// more hexadecimal digits of either case, optionally with leading and trailing whitespace.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a hexadecimal number; otherwise <see langword="false"/>.</returns>
    private static bool IsHexNumber(ReadOnlySpan<char> value)
    {
        var index = 0;

        while (index < value.Length && IsWhiteSpace(value[index]))
        {
            index++;
        }

        var digitsStart = index;

        while (index < value.Length && char.IsAsciiHexDigit(value[index]))
        {
            index++;
        }

        if (index == digitsStart)
        {
            return false;
        }

        while (index < value.Length && IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index == value.Length;

        // The characters that number parsing treats as whitespace.
        static bool IsWhiteSpace(char c)
        {
            return c is ' ' or (>= '\t' and <= '\r');
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck.Xunit;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.AWS.Trace;

namespace OpenTelemetry.Extensions.AWS.FuzzTests;

public static class AWSXRayPropagatorTests
{
    private const int MaxTest = 1_000;
    private const string HeaderName = "X-Amzn-Trace-Id";

    private static readonly AWSXRayPropagator Propagator = new();

    [Property(MaxTest = MaxTest)]
    public static void TryParseXRayTraceHeader_Arbitrary_Input_Does_Not_Throw(string? header)
    {
        var exception = Record.Exception(() => AWSXRayPropagator.TryParseXRayTraceHeader(header!, out _));

        Assert.True(exception is null, $"{exception?.GetType().Name}: {exception?.Message}");
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(XRayGenerators)])]
    public static void TryParseXRayTraceHeader_Structured_Input_Does_Not_Throw(XRayHeaderCase value)
    {
        // This is a "Try" method, so a header which is not valid must be reported by its result rather than
        // by an exception, which the caller would otherwise have to catch for every such request.
        var exception = Record.Exception(() => AWSXRayPropagator.TryParseXRayTraceHeader(value.Header, out _));

        Assert.True(exception is null, $"{exception?.GetType().Name}: {exception?.Message} for {value}");
    }

    [Property(MaxTest = MaxTest)]
    public static void Extract_Arbitrary_Header_Returns_Only_Canonical_Contexts(string? header) =>
        AssertExtractedContextIsCanonical(header);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(XRayGenerators)])]
    public static void Extract_Structured_Header_Returns_Only_Canonical_Contexts(XRayHeaderCase value) =>
        AssertExtractedContextIsCanonical(value.Header);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(XRayGenerators)])]
    public static void Extract_Reads_Back_A_Well_Formed_Header(XRayHeaderCase value)
    {
        if (value.TraceId is null)
        {
            return;
        }

        var extracted = Extract(value.Header);

        Assert.True(extracted.ActivityContext.IsValid(), value.ToString());
        Assert.Equal(value.TraceId, extracted.ActivityContext.TraceId.ToHexString());
        Assert.Equal(value.SpanId, extracted.ActivityContext.SpanId.ToHexString());
        Assert.Equal(value.Sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None, extracted.ActivityContext.TraceFlags);
        Assert.True(extracted.ActivityContext.IsRemote);
    }

    [Property(MaxTest = MaxTest)]
    public static void Inject_Then_Extract_Round_Trips(byte[] traceBytes, byte[] spanBytes, bool sampled)
    {
        var traceId = ActivityTraceId.CreateFromBytes(Pad(traceBytes, 16));
        var spanId = ActivitySpanId.CreateFromBytes(Pad(spanBytes, 8));
        var context = new ActivityContext(traceId, spanId, sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
        var carrier = new Dictionary<string, string>();

        Propagator.Inject(new PropagationContext(context, default), carrier, static (c, k, v) => c[k] = v);

        var extracted = Propagator.Extract(default, carrier, static (c, k) => c.TryGetValue(k, out var v) ? [v] : null);

        Assert.Equal(traceId, extracted.ActivityContext.TraceId);
        Assert.Equal(spanId, extracted.ActivityContext.SpanId);
        Assert.Equal(context.TraceFlags, extracted.ActivityContext.TraceFlags);
        Assert.True(extracted.ActivityContext.IsRemote);

        var trace = traceId.ToHexString();
        Assert.Equal($"Root=1-{trace.Substring(0, 8)}-{trace.Substring(8)};Parent={spanId.ToHexString()};Sampled={(sampled ? '1' : '0')}", carrier[HeaderName]);
    }

    private static PropagationContext Extract(string? header)
    {
        var carrier = new Dictionary<string, string>();
        if (header != null)
        {
            carrier[HeaderName] = header;
        }

        return Propagator.Extract(default, carrier, static (c, k) => c.TryGetValue(k, out var v) ? [v] : null);
    }

    private static void AssertExtractedContextIsCanonical(string? header)
    {
        var extracted = Extract(header);

        if (!extracted.ActivityContext.IsValid())
        {
            return;
        }

        // Whatever was accepted must be written exactly as X-Ray writes it: lower case hexadecimal digits of
        // the right length with no padding, and a sampling decision of 0 or 1.
        var trace = extracted.ActivityContext.TraceId.ToHexString();
        var sampled = extracted.ActivityContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? '1' : '0';

        Assert.Contains($"Root=1-{trace.Substring(0, 8)}-{trace.Substring(8)}", header, StringComparison.Ordinal);
        Assert.Contains($"Parent={extracted.ActivityContext.SpanId.ToHexString()}", header, StringComparison.Ordinal);
        Assert.Contains($"Sampled={sampled}", header, StringComparison.Ordinal);
    }

    private static byte[] Pad(byte[] bytes, int length)
    {
        // A trace or span identifier of all zeros is not valid, so the last byte is never zero.
        var result = new byte[length];
        Array.Copy(bytes, result, Math.Min(bytes.Length, length));
        result[length - 1] |= 1;
        return result;
    }
}

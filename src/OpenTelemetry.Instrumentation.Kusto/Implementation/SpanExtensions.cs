// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class SpanExtensions
{
    public static ReadOnlySpan<char> SliceAfter(this ReadOnlySpan<char> span, ReadOnlySpan<char> needle)
    {
        var idx = span.IndexOf(needle);
        return idx >= 0 ? span.Slice(idx + needle.Length) : [];
    }

    public static ReadOnlySpan<char> SliceBefore(this ReadOnlySpan<char> span, ReadOnlySpan<char> needle)
    {
        var idx = span.IndexOf(needle);
        return idx >= 0 ? span.Slice(0, idx) : [];
    }
}

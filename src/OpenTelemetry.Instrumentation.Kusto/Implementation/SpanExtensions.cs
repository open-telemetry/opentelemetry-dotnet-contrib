// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Extension methods for <see cref="ReadOnlySpan{T}"/>.
/// </summary>
internal static class SpanExtensions
{
    /// <summary>
    /// Slices the span after the first occurrence of the <paramref name="needle"/>.
    /// </summary>
    /// <param name="span">
    /// The span to slice.
    /// </param>
    /// <param name="needle">
    /// The value to search for.
    /// </param>
    /// <returns>
    /// A <see cref="ReadOnlySpan{T}"/> that is a slice of the original span after the first occurrence of the <paramref name="needle"/>.
    /// </returns>
    public static ReadOnlySpan<char> SliceAfter(this ReadOnlySpan<char> span, ReadOnlySpan<char> needle)
    {
        var idx = span.IndexOf(needle);
        return idx >= 0 ? span.Slice(idx + needle.Length) : [];
    }
}

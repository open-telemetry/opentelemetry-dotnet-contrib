// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Stackdriver.Utils;

/// <summary>
/// Common Utility Methods that are not metrics/trace specific.
/// </summary>
public static class CommonUtils
{
    /// <summary>
    /// Divide the source list into batches of lists of given size.
    /// </summary>
    /// <typeparam name="T">The type of the list.</typeparam>
    /// <param name="source">The list.</param>
    /// <param name="size">Size of the batch.</param>
    /// <returns><see cref="IEnumerable{T}"/>.</returns>
    public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> source, int size)
    {
        Guard.ThrowIfNull(source);

        using var enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return WalkPartition(enumerator, size - 1);
        }
    }

    private static IEnumerable<T> WalkPartition<T>(IEnumerator<T> source, int size)
    {
        yield return source.Current;
        for (var i = 0; i < size && source.MoveNext(); i++)
        {
            yield return source.Current;
        }
    }
}

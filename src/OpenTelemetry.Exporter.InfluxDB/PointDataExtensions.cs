// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using InfluxDB.Client.Writes;

namespace OpenTelemetry.Exporter.InfluxDB;

internal static class PointDataExtensions
{
    public static PointData Tags(this PointData pointData, ReadOnlyTagCollection tags)
    {
        foreach (var tag in tags)
        {
            pointData = pointData.EscapedTag(tag.Key, tag.Value?.ToString());
        }

        return pointData;
    }

    public static PointData Tags<T>(this PointData pointData, IEnumerable<KeyValuePair<string, T>>? tags)
    {
        if (tags == null)
        {
            return pointData;
        }

        foreach (var tag in tags)
        {
            pointData = pointData.EscapedTag(tag.Key, tag.Value?.ToString());
        }

        return pointData;
    }

    public static void WriteTo(this PointData pointData, List<string> lineProtocol, string metricName)
    {
        var line = pointData.ToLineProtocol();

        if (line.Length == 0)
        {
            InfluxDBEventSource.Log.DataPointDropped(metricName);
        }
        else
        {
            lineProtocol.Add(line);
        }
    }

    /// <summary>
    /// Escapes the backslashes in a tag key or value that InfluxDB.Client would
    /// otherwise emit in a way that corrupts the line protocol record.
    /// </summary>
    /// <param name="value">The tag key or value.</param>
    /// <returns>The escaped key or value.</returns>
    internal static string EscapeTagComponent(string value)
    {
        var index =
#if NET
            value.IndexOf('\\', StringComparison.Ordinal);
#else
            value.IndexOf('\\');
#endif

        if (index < 0)
        {
            return value;
        }

        StringBuilder? builder = null;
        var copiedUpTo = 0;

        while (index < value.Length)
        {
            if (value[index] != '\\')
            {
                index++;
                continue;
            }

            var runStart = index;
            while (index < value.Length && value[index] == '\\')
            {
                index++;
            }

            var isOddRun = ((index - runStart) & 1) == 1;
            var isFollowedByEscapedCharacter = index == value.Length || value[index] is ' ' or ',' or '=';

            if (isOddRun && isFollowedByEscapedCharacter)
            {
                builder ??= new(value.Length + 4);
                builder.Append(value, copiedUpTo, index - copiedUpTo).Append('\\');
                copiedUpTo = index;
            }
        }

        return builder == null ? value : builder.Append(value, copiedUpTo, value.Length - copiedUpTo).ToString();
    }

    private static PointData EscapedTag(this PointData pointData, string key, string? value)
        => pointData.Tag(EscapeTagComponent(key), value == null ? null : EscapeTagComponent(value));
}

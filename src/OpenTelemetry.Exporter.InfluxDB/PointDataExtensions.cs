// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// <para/>
    /// Backslashes are doubled before InfluxDB.Client escapes spaces, commas and
    /// equals signs. InfluxDB 1.x and 2.x consider a tag delimiter escaped whenever
    /// its preceding character is a backslash, regardless of the backslash count.
    /// A trailing space is therefore appended to keys and values ending in a
    /// backslash so that the following delimiter can be recognized.
    /// </summary>
    /// <param name="value">The tag key or value.</param>
    /// <returns>The escaped key or value.</returns>
    internal static string EscapeTagComponent(string value)
    {
#if NET
        if (!value.Contains('\\', StringComparison.Ordinal))
#else
        if (value.IndexOf('\\') < 0)
#endif
        {
            return value;
        }

#if NET
        var escaped = value.Replace(@"\", @"\\", StringComparison.Ordinal);
#else
        var escaped = value.Replace(@"\", @"\\");
#endif

        return value[value.Length - 1] == '\\' ? escaped + " " : escaped;
    }

    private static PointData EscapedTag(this PointData pointData, string key, string? value)
        => pointData.Tag(EscapeTagComponent(key), value == null ? null : EscapeTagComponent(value));
}

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
    /// Line protocol parsers interpret two contiguous backslashes as a single
    /// literal backslash, so every backslash is doubled to preserve the original
    /// value. This also ensures a trailing backslash, or one preceding a space,
    /// comma or equals sign that InfluxDB.Client escapes, cannot escape the delimiter.
    /// </summary>
    /// <param name="value">The tag key or value.</param>
    /// <returns>The escaped key or value.</returns>
    internal static string EscapeTagComponent(string value)
#if NET
        => value.Contains('\\', StringComparison.Ordinal) ? value.Replace(@"\", @"\\", StringComparison.Ordinal) : value;
#else
        => value.IndexOf('\\') < 0 ? value : value.Replace(@"\", @"\\");
#endif

    private static PointData EscapedTag(this PointData pointData, string key, string? value)
        => pointData.Tag(EscapeTagComponent(key), value == null ? null : EscapeTagComponent(value));
}

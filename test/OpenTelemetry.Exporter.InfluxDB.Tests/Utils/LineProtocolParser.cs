// <copyright file="LineProtocolParser.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Globalization;

namespace OpenTelemetry.Exporter.InfluxDB.Tests.Utils;

public class LineProtocolParser
{
    private static readonly DateTime UnixEpoch = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc);

    public static PointData ParseLine(string line)
    {
        var segments = line.Split(' ');
        if (segments.Length is < 2 or > 3)
        {
            throw new ArgumentException("Invalid InfluxDB Line Protocol format.", nameof(line));
        }

        var measurementAndTags = segments[0];
        var fieldsSection = segments[1];
        var timestampSection = segments.Length == 3 ? segments[2] : null;

        var measurementAndTagsParts = measurementAndTags.Split(',');
        var measurement = measurementAndTagsParts[0];
        var tags = ParseTags(measurementAndTagsParts.Skip(1));
        var fields = ParseFields(fieldsSection);
        var timestamp = ParseTimestamp(timestampSection);

        return new PointData { Measurement = measurement, Tags = tags, Fields = fields, Timestamp = timestamp };
    }

    private static Dictionary<string, object> ParseFields(string fieldsSection)
    {
        return fieldsSection.Split(',')
            .Where(field => !string.IsNullOrEmpty(field))
            .Select(field =>
            {
                var kv = field.Split('=');
                var key = kv[0];
                var value = ParseFieldValue(kv[1]);
                return new KeyValuePair<string, object>(key, value);
            })
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private static List<KeyValuePair<string, string>> ParseTags(IEnumerable<string> tagParts)
    {
        return tagParts
            .Select(tagPart =>
            {
                var kv = tagPart.Split('=');
                return new KeyValuePair<string, string>(kv[0], kv[1]);
            })
            .ToList();
    }

    private static object ParseFieldValue(string fieldValue)
    {
        if (bool.TryParse(fieldValue, out bool boolValue))
        {
            return boolValue;
        }

        if (fieldValue.EndsWith("i", StringComparison.Ordinal)
            && long.TryParse(fieldValue.AsSpan(0, fieldValue.Length - 1).ToString(), out long intValue))
        {
            return intValue;
        }

        if (double.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return doubleValue;
        }

        return fieldValue;
    }

    private static DateTime ParseTimestamp(string? timestampSection)
    {
        if (string.IsNullOrEmpty(timestampSection) || !long.TryParse(timestampSection, out long unixTimeNanoseconds))
        {
            throw new ArgumentException("Invalid formatted timestamp.");
        }

        long ticks = unixTimeNanoseconds / 100;
        return UnixEpoch.AddTicks(ticks);
    }
}

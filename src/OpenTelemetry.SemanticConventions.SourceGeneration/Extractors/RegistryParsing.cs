// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

internal static class RegistryParsing
{
    public static DeprecatedModel? ParseDeprecated(JsonObject? obj)
    {
        if (obj is null) return null;

        var reason = obj.GetString("reason");
        var note = obj.GetString("note");

        return reason switch
        {
            "renamed" => new DeprecatedModel.Renamed(obj.GetString("renamed_to"), note),
            "obsoleted" => new DeprecatedModel.Obsoleted(note),
            _ => new DeprecatedModel.Uncategorized(note)
        };
    }

    public static EquatableArray<string> ParseExamples(JsonArray? examplesArr)
    {
        if (examplesArr is null)
            return default;

        var examples = new List<string>(examplesArr.Items.Count);
        foreach (var value in examplesArr.Items)
            examples.Add(value is JsonString s ? s.Value : ToCompactJson(value));

        return examples.ToEquatableArray();
    }

    public static RequirementLevelModel ParseRequirementLevel(JsonValue? value)
    {
        if (value is JsonString s)
            return new RequirementLevelModel(ParseRequirementKind(s.Value), string.Empty);

        if (value is JsonObject obj)
        {
            foreach (var pair in obj.Members)
            {
                var condition = pair.Value is JsonString conditionString
                    ? conditionString.Value
                    : ToCompactJson(pair.Value);
                return new RequirementLevelModel(ParseRequirementKind(pair.Key), condition);
            }
        }

        return new RequirementLevelModel(RequirementLevelKind.Unspecified, string.Empty);
    }

    private static RequirementLevelKind ParseRequirementKind(string value) => value switch
    {
        "required" => RequirementLevelKind.Required,
        "recommended" => RequirementLevelKind.Recommended,
        "opt_in" => RequirementLevelKind.OptIn,
        "conditionally_required" => RequirementLevelKind.ConditionallyRequired,
        _ => RequirementLevelKind.Unspecified
    };

    public static string ToCompactJson(JsonValue value) => value switch
    {
        JsonNull => "null",
        JsonBool b => b.Value ? "true" : "false",
        JsonNumber n => n.Raw,
        JsonString s => "\"" + EscapeJsonString(s.Value) + "\"",
        JsonArray a => "[" + string.Join(",", a.Items.Select(ToCompactJson)) + "]",
        JsonObject o => "{" + string.Join(",", o.Members.Select(static pair =>
            "\"" + EscapeJsonString(pair.Key) + "\":" + ToCompactJson(pair.Value))) + "}",
        _ => string.Empty
    };

    private static string EscapeJsonString(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (char.IsControl(ch))
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }
        return builder.ToString();
    }
}

/// <summary>
/// Unit-to-value-type mapping for OpenTelemetry instruments. The rules follow
/// the OTel <c>instrument</c> + UCUM-style <c>unit</c> convention:
/// continuous units (<c>s</c>, <c>ms</c>, <c>By</c>, <c>1</c>, <c>%</c>) project
/// to <see cref="double"/>; counting units (<c>{request}</c>, <c>{count}</c>)
/// project to <see cref="long"/>. Gauges are always observable and default to
/// <see cref="double"/>.
///
/// State-style metrics (<c>http.client.active_requests</c>,
/// <c>http.server.active_requests</c>, etc.) follow the upstream
/// <c>updowncounter</c> pattern, not a guessed Gauge. The meter emitter
/// preserves the registry's declared <c>instrument</c> verbatim.
/// </summary>
internal static class MeterValueTypeRules
{
    public static string SelectValueType(string instrument, string unit)
    {
        if (string.Equals(instrument, "gauge", StringComparison.Ordinal) ||
            string.Equals(instrument, "observablegauge", StringComparison.Ordinal))
            return "double";

        return unit switch
        {
            "s" or "ms" or "us" or "ns" => "double",
            "By" => "long",
            "%" => "double",
            "1" => "double",
            _ => IsCountingUnit(unit) ? "long" : "double"
        };
    }

    private static bool IsCountingUnit(string unit)
        => !string.IsNullOrEmpty(unit) && unit.StartsWith("{", StringComparison.Ordinal);
}

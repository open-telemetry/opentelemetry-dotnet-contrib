// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// How a DynamoDB request or response parameter is converted to an attribute value.
/// </summary>
internal enum DynamoDbValueKind
{
    /// <summary>A string, or the value of an SDK <see cref="ConstantClass"/>.</summary>
    String,

    Bool,

    /// <summary>An integer, recorded as a <see cref="long"/>.</summary>
    Long,

    /// <summary>A number, recorded as a <see cref="double"/>.</summary>
    Double,

    /// <summary>A list of strings, or a list of objects each serialized to JSON.</summary>
    StringArray,

    /// <summary>The number of items in a collection.</summary>
    Count,

    /// <summary>The keys of a dictionary.</summary>
    Keys,

    Json,
}

/// <summary>
/// A DynamoDB request or response parameter, by property path (for example
/// <c>ProvisionedThroughput.ReadCapacityUnits</c>), and how its value is converted.
/// </summary>
internal readonly struct DynamoDbParameter
{
    public DynamoDbParameter(string name, DynamoDbValueKind kind)
    {
        this.Name = name;
        this.Path = name.Split('.');
        this.Kind = kind;
    }

    public string Name { get; }

    public string[] Path { get; }

    public DynamoDbValueKind Kind { get; }
}

/// <summary>
/// Adds the <c>aws.dynamodb.*</c> attributes for the parameters listed in
/// <see cref="AWSServiceHelper.DynamoDbRequestParameterMap"/> and
/// <see cref="AWSServiceHelper.DynamoDbResponseParameterMap"/>.
/// </summary>
internal static class DynamoDbAttributeHelper
{
    internal static void AddAttributes(
        Activity activity,
        IReadOnlyDictionary<string, DynamoDbParameter[]> parameterMap,
        string operationName,
        object source,
        IDictionary<string, string> parameterAttributeMap)
    {
        if (!parameterMap.TryGetValue(operationName, out var parameters))
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            try
            {
                if (parameterAttributeMap.TryGetValue(parameter.Name, out var attribute)
                    && GetValue(source, parameter.Path) is { } rawValue
                    && ConvertValue(rawValue, parameter.Kind) is { } value)
                {
                    activity.SetTag(attribute, value);
                }
            }
            catch (Exception)
            {
                // Guard against any reflection-related exceptions when running in AoT.
                // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1543#issuecomment-1907667722.
            }
        }
    }

    private static object? GetValue(object source, string[] path)
    {
        var current = (object?)source;
        foreach (var name in path)
        {
            if (current == null)
            {
                return null;
            }

            current = AWSTracingPipelineHandler.GetCachedProperty(current.GetType(), name)?.GetValue(current);
        }

        return current;
    }

    private static object? ConvertValue(object value, DynamoDbValueKind kind) => kind switch
    {
        DynamoDbValueKind.String => value switch
        {
            ConstantClass constant => NullIfEmpty(constant.Value),
            string s => NullIfEmpty(s),
            _ => null,
        },
        DynamoDbValueKind.Bool => value as bool?,
        DynamoDbValueKind.Long => value is int i ? (long)i : null,
        DynamoDbValueKind.Double => value is long l ? (double)l : null,
        DynamoDbValueKind.Count => value is ICollection collection ? (long)collection.Count : null,
        DynamoDbValueKind.Keys => value is IDictionary dictionary
            ? NullIfEmpty(dictionary.Keys.Cast<string>().ToArray())
            : null,
        DynamoDbValueKind.StringArray => ToStringArray(value),
        DynamoDbValueKind.Json => AWSModelJsonSerializer.Serialize(value),
        _ => null,
    };

    private static string[]? ToStringArray(object value)
    {
        if (value is not IEnumerable)
        {
            return AWSModelJsonSerializer.Serialize(value) is { } json ? [json] : null;
        }

        var items = new List<string>();
        foreach (var item in (IEnumerable)value)
        {
            var text = item as string ?? AWSModelJsonSerializer.Serialize(item);
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(text!);
            }
        }

        return NullIfEmpty(items.ToArray());
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string[]? NullIfEmpty(string[] value) => value.Length == 0 ? null : value;
}

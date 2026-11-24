// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Adds the <c>aws.dynamodb.*</c> attributes the semantic conventions define per DynamoDB operation.
/// See https://github.com/open-telemetry/semantic-conventions/blob/main/docs/db/dynamodb.md.
/// <para/>
/// <c>aws.dynamodb.table_names</c> from <c>TableName</c> is set by the generic request parameter mapping.
/// </summary>
internal static class DynamoDbAttributes
{
    private static readonly IReadOnlyDictionary<string, Mapping[]> RequestMappings = new Dictionary<string, Mapping[]>(StringComparer.Ordinal)
    {
        ["BatchGetItem"] = [new("aws.dynamodb.table_names", "RequestItems", Kind.Keys)],
        ["BatchWriteItem"] = [new("aws.dynamodb.table_names", "RequestItems", Kind.Keys)],
        ["CreateTable"] =
        [
            new("aws.dynamodb.global_secondary_indexes", "GlobalSecondaryIndexes", Kind.StringArray),
            new("aws.dynamodb.local_secondary_indexes", "LocalSecondaryIndexes", Kind.StringArray),
            new("aws.dynamodb.provisioned_read_capacity", "ProvisionedThroughput.ReadCapacityUnits", Kind.Double),
            new("aws.dynamodb.provisioned_write_capacity", "ProvisionedThroughput.WriteCapacityUnits", Kind.Double),
        ],
        ["GetItem"] =
        [
            new("aws.dynamodb.consistent_read", "ConsistentRead", Kind.Bool),
            new("aws.dynamodb.projection", "ProjectionExpression", Kind.String),
        ],
        ["ListTables"] =
        [
            new("aws.dynamodb.exclusive_start_table", "ExclusiveStartTableName", Kind.String),
            new("aws.dynamodb.limit", "Limit", Kind.Long),
        ],
        ["Query"] =
        [
            new("aws.dynamodb.attributes_to_get", "AttributesToGet", Kind.StringArray),
            new("aws.dynamodb.consistent_read", "ConsistentRead", Kind.Bool),
            new("aws.dynamodb.index_name", "IndexName", Kind.String),
            new("aws.dynamodb.limit", "Limit", Kind.Long),
            new("aws.dynamodb.projection", "ProjectionExpression", Kind.String),
            new("aws.dynamodb.scan_forward", "ScanIndexForward", Kind.Bool),
            new("aws.dynamodb.select", "Select", Kind.String),
        ],
        ["Scan"] =
        [
            new("aws.dynamodb.attributes_to_get", "AttributesToGet", Kind.StringArray),
            new("aws.dynamodb.consistent_read", "ConsistentRead", Kind.Bool),
            new("aws.dynamodb.index_name", "IndexName", Kind.String),
            new("aws.dynamodb.limit", "Limit", Kind.Long),
            new("aws.dynamodb.projection", "ProjectionExpression", Kind.String),
            new("aws.dynamodb.segment", "Segment", Kind.Long),
            new("aws.dynamodb.select", "Select", Kind.String),
            new("aws.dynamodb.total_segments", "TotalSegments", Kind.Long),
        ],
        ["UpdateTable"] =
        [
            new("aws.dynamodb.attribute_definitions", "AttributeDefinitions", Kind.StringArray),
            new("aws.dynamodb.global_secondary_index_updates", "GlobalSecondaryIndexUpdates", Kind.StringArray),
            new("aws.dynamodb.provisioned_read_capacity", "ProvisionedThroughput.ReadCapacityUnits", Kind.Double),
            new("aws.dynamodb.provisioned_write_capacity", "ProvisionedThroughput.WriteCapacityUnits", Kind.Double),
        ],
    };

    private static readonly Mapping ConsumedCapacity = new("aws.dynamodb.consumed_capacity", "ConsumedCapacity", Kind.StringArray);
    private static readonly Mapping ItemCollectionMetrics = new("aws.dynamodb.item_collection_metrics", "ItemCollectionMetrics", Kind.Json);

    private static readonly IReadOnlyDictionary<string, Mapping[]> ResponseMappings = new Dictionary<string, Mapping[]>(StringComparer.Ordinal)
    {
        ["BatchGetItem"] = [ConsumedCapacity],
        ["BatchWriteItem"] = [ConsumedCapacity, ItemCollectionMetrics],
        ["CreateTable"] = [ConsumedCapacity, ItemCollectionMetrics],
        ["DeleteItem"] = [ConsumedCapacity, ItemCollectionMetrics],
        ["GetItem"] = [ConsumedCapacity],
        ["ListTables"] = [new("aws.dynamodb.table_count", "TableNames", Kind.Count)],
        ["PutItem"] = [ConsumedCapacity, ItemCollectionMetrics],
        ["Query"] = [ConsumedCapacity],
        ["Scan"] =
        [
            ConsumedCapacity,
            new("aws.dynamodb.count", "Count", Kind.Long),
            new("aws.dynamodb.scanned_count", "ScannedCount", Kind.Long),
        ],
        ["UpdateItem"] = [ConsumedCapacity, ItemCollectionMetrics],
        ["UpdateTable"] = [ConsumedCapacity],
    };

    private enum Kind
    {
        String,
        Bool,
        Long,
        Double,
        StringArray,
        Count,
        Keys,
        Json,
    }

    internal static void AddRequestAttributes(Activity activity, string operation, object request)
        => AddAttributes(activity, RequestMappings, operation, request);

    internal static void AddResponseAttributes(Activity activity, string operation, object response)
        => AddAttributes(activity, ResponseMappings, operation, response);

    private static void AddAttributes(Activity activity, IReadOnlyDictionary<string, Mapping[]> mappings, string operation, object source)
    {
        if (!mappings.TryGetValue(operation, out var operationMappings))
        {
            return;
        }

        foreach (var mapping in operationMappings)
        {
            try
            {
                var value = Convert(GetValue(source, mapping.Path), mapping.Kind);
                if (value != null)
                {
                    activity.SetTag(mapping.Attribute, value);
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

    private static object? Convert(object? value, Kind kind) => value switch
    {
        null => null,
        _ => kind switch
        {
            Kind.String => value switch
            {
                ConstantClass constant => NullIfEmpty(constant.Value),
                string s => NullIfEmpty(s),
                _ => null,
            },
            Kind.Bool => value as bool?,
            Kind.Long => value is int or long ? System.Convert.ToInt64(value, CultureInfo.InvariantCulture) : null,
            Kind.Double => value is int or long or double ? System.Convert.ToDouble(value, CultureInfo.InvariantCulture) : null,
            Kind.Count => value is ICollection collection ? (long)collection.Count : null,
            Kind.Keys => value is IDictionary dictionary ? NullIfEmpty(dictionary.Keys.Cast<object>().Select(k => k.ToString()!).ToArray()) : null,
            Kind.StringArray => ToStringArray(value),
            Kind.Json => AwsSdkModelJsonSerializer.Serialize(value),
            _ => null,
        },
    };

    private static string[]? ToStringArray(object value)
    {
        if (value is string or not IEnumerable)
        {
            return AwsSdkModelJsonSerializer.Serialize(value) is { } json ? [json] : null;
        }

        var items = new List<string>();
        foreach (var item in (IEnumerable)value)
        {
            var text = item as string ?? AwsSdkModelJsonSerializer.Serialize(item);
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(text!);
            }
        }

        return NullIfEmpty(items.ToArray());
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string[]? NullIfEmpty(string[] value) => value.Length == 0 ? null : value;

    private sealed class Mapping
    {
        public Mapping(string attribute, string path, Kind kind)
        {
            this.Attribute = attribute;
            this.Path = path.Split('.');
            this.Kind = kind;
        }

        public string Attribute { get; }

        public string[] Path { get; }

        public Kind Kind { get; }
    }
}

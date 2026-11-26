// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;

// Theory rows build their expected tag arrays inline for readability.
#pragma warning disable CA1861

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public sealed class TestAWSClientDynamoDbAttributes : IDisposable
{
    private const string ConsumedCapacityJson = "{\"TableName\":\"T\",\"CapacityUnits\":1.0}";
    private const string ExpectedConsumedCapacity = "{\"CapacityUnits\":1,\"TableName\":\"T\"}";

    private const string ItemCollectionMetricsJson =
        "{\"ItemCollectionKey\":{\"pk\":{\"S\":\"a\"}},\"SizeEstimateRangeGB\":[1.0,2.0]}";

    private const string ExpectedItemCollectionMetrics =
        "{\"ItemCollectionKey\":{\"pk\":{\"S\":\"a\"}},\"SizeEstimateRangeGB\":[1,2]}";

    private const string ItemResponseJson =
        "{\"ConsumedCapacity\":" + ConsumedCapacityJson
        + ",\"ItemCollectionMetrics\":" + ItemCollectionMetricsJson + "}";

    private static readonly string[] TableNames = ["T"];

    public static TheoryData<string, AmazonDynamoDBRequest, string, Dictionary<string, object>> Operations => new()
    {
        {
            "Query",
            new QueryRequest
            {
                TableName = "T",
                IndexName = "Idx",
                Limit = 10,
                ConsistentRead = true,
                ScanIndexForward = false,
                Select = Select.SPECIFIC_ATTRIBUTES,
                ProjectionExpression = "a,b",
                AttributesToGet = ["a"],
            },
            "{\"ConsumedCapacity\":" + ConsumedCapacityJson + "}",
            new()
            {
                ["aws.dynamodb.table_names"] = TableNames,
                ["aws.dynamodb.index_name"] = "Idx",
                ["aws.dynamodb.limit"] = 10L,
                ["aws.dynamodb.consistent_read"] = true,
                ["aws.dynamodb.scan_forward"] = false,
                ["aws.dynamodb.select"] = "SPECIFIC_ATTRIBUTES",
                ["aws.dynamodb.projection"] = "a,b",
                ["aws.dynamodb.attributes_to_get"] = new[] { "a" },
                ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity },
            }
        },
        {
            "Scan",
            new ScanRequest
            {
                TableName = "T",
                IndexName = "Idx",
                Limit = 5,
                ConsistentRead = false,
                Segment = 1,
                TotalSegments = 4,
                Select = Select.ALL_ATTRIBUTES,
                ProjectionExpression = "a",
                AttributesToGet = ["a", "b"],
            },
            "{\"Count\":2,\"ScannedCount\":5,\"ConsumedCapacity\":" + ConsumedCapacityJson + "}",
            new()
            {
                ["aws.dynamodb.table_names"] = TableNames,
                ["aws.dynamodb.index_name"] = "Idx",
                ["aws.dynamodb.limit"] = 5L,
                ["aws.dynamodb.consistent_read"] = false,
                ["aws.dynamodb.segment"] = 1L,
                ["aws.dynamodb.total_segments"] = 4L,
                ["aws.dynamodb.select"] = "ALL_ATTRIBUTES",
                ["aws.dynamodb.projection"] = "a",
                ["aws.dynamodb.attributes_to_get"] = new[] { "a", "b" },
                ["aws.dynamodb.count"] = 2L,
                ["aws.dynamodb.scanned_count"] = 5L,
                ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity },
            }
        },
        {
            "Scan",
            new ScanRequest { TableName = "T", AttributesToGet = [] },
            "{}",
            new() { ["aws.dynamodb.table_names"] = TableNames }
        },
        {
            "GetItem",
            new GetItemRequest { TableName = "T", ConsistentRead = true, ProjectionExpression = "a" },
            "{\"ConsumedCapacity\":" + ConsumedCapacityJson + "}",
            new()
            {
                ["aws.dynamodb.table_names"] = TableNames,
                ["aws.dynamodb.consistent_read"] = true,
                ["aws.dynamodb.projection"] = "a",
                ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity },
            }
        },
        {
            "BatchGetItem",
            new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    ["A"] = new() { ProjectionExpression = "a" },
                    ["B"] = new() { ProjectionExpression = "b" },
                },
            },
            "{\"ConsumedCapacity\":[" + ConsumedCapacityJson + "," + ConsumedCapacityJson + "]}",
            new()
            {
                ["aws.dynamodb.table_names"] = new[] { "A", "B" },
                ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity, ExpectedConsumedCapacity },
            }
        },
        {
            "BatchWriteItem",
            new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    ["A"] = [new() { DeleteRequest = new() }],
                    ["B"] = [new() { DeleteRequest = new() }],
                },
            },
            "{\"ConsumedCapacity\":[" + ConsumedCapacityJson + "],"
                + "\"ItemCollectionMetrics\":{\"A\":[" + ItemCollectionMetricsJson + "]}}",
            new()
            {
                ["aws.dynamodb.table_names"] = new[] { "A", "B" },
                ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity },
                ["aws.dynamodb.item_collection_metrics"] = "{\"A\":[" + ExpectedItemCollectionMetrics + "]}",
            }
        },
        { "PutItem", new PutItemRequest { TableName = "T" }, ItemResponseJson, ItemTags() },
        { "UpdateItem", new UpdateItemRequest { TableName = "T" }, ItemResponseJson, ItemTags() },
        { "DeleteItem", new DeleteItemRequest { TableName = "T" }, ItemResponseJson, ItemTags() },
        {
            "CreateTable",
            new CreateTableRequest
            {
                TableName = "T",
                GlobalSecondaryIndexes =
                [
                    new()
                    {
                        IndexName = "Gsi1",
                        KeySchema = [new() { AttributeName = "pk", KeyType = KeyType.HASH }],
                        Projection = new() { ProjectionType = ProjectionType.ALL },
                    },
                ],
                LocalSecondaryIndexes =
                [
                    new()
                    {
                        IndexName = "Lsi1",
                        KeySchema = [new() { AttributeName = "sk", KeyType = KeyType.RANGE }],
                        Projection = new() { ProjectionType = ProjectionType.KEYS_ONLY },
                    },
                ],
                ProvisionedThroughput = new() { ReadCapacityUnits = 5, WriteCapacityUnits = 7 },
            },
            "{}",
            new()
            {
                ["aws.dynamodb.table_names"] = TableNames,
                ["aws.dynamodb.global_secondary_indexes"] = new[]
                {
                    "{\"IndexName\":\"Gsi1\",\"KeySchema\":[{\"AttributeName\":\"pk\",\"KeyType\":\"HASH\"}],"
                        + "\"Projection\":{\"ProjectionType\":\"ALL\"}}",
                },
                ["aws.dynamodb.local_secondary_indexes"] = new[]
                {
                    "{\"IndexName\":\"Lsi1\",\"KeySchema\":[{\"AttributeName\":\"sk\",\"KeyType\":\"RANGE\"}],"
                        + "\"Projection\":{\"ProjectionType\":\"KEYS_ONLY\"}}",
                },
                ["aws.dynamodb.provisioned_read_capacity"] = 5.0,
                ["aws.dynamodb.provisioned_write_capacity"] = 7.0,
            }
        },
        {
            "UpdateTable",
            new UpdateTableRequest
            {
                TableName = "T",
                AttributeDefinitions = [new() { AttributeName = "pk", AttributeType = ScalarAttributeType.S }],
                GlobalSecondaryIndexUpdates = [new() { Delete = new() { IndexName = "Gsi1" } }],
                ProvisionedThroughput = new() { ReadCapacityUnits = 1, WriteCapacityUnits = 2 },
            },
            "{}",
            new()
            {
                ["aws.dynamodb.table_names"] = TableNames,
                ["aws.dynamodb.attribute_definitions"] = new[] { "{\"AttributeName\":\"pk\",\"AttributeType\":\"S\"}" },
                ["aws.dynamodb.global_secondary_index_updates"] = new[] { "{\"Delete\":{\"IndexName\":\"Gsi1\"}}" },
                ["aws.dynamodb.provisioned_read_capacity"] = 1.0,
                ["aws.dynamodb.provisioned_write_capacity"] = 2.0,
            }
        },
        {
            "ListTables",
            new ListTablesRequest { ExclusiveStartTableName = "Xyz", Limit = 3 },
            "{\"TableNames\":[\"a\",\"b\"]}",
            new()
            {
                ["aws.dynamodb.exclusive_start_table"] = "Xyz",
                ["aws.dynamodb.limit"] = 3L,
                ["aws.dynamodb.table_count"] = 2L,
            }
        },
        {
            // Not defined by the semantic conventions, so the response's consumed capacity is not recorded.
            "TransactGetItems",
            new TransactGetItemsRequest(),
            "{\"ConsumedCapacity\":[" + ConsumedCapacityJson + "]}",
            new()
        },
    };

    [Theory]
    [MemberData(nameof(Operations), DisableDiscoveryEnumeration = true)]
#if NETFRAMEWORK
    public void EmitsSemanticConventionAttributes(
#else
    public async Task EmitsSemanticConventionAttributes(
#endif
        string operation,
        AmazonDynamoDBRequest request,
        string responseJson,
        Dictionary<string, object> expected,
        SemanticConventionVersion version = SemanticConventionVersion.Latest)
    {
        var exportedItems = new List<Activity>();

        using (Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new AlwaysOnSampler())
                   .AddAWSInstrumentation(o => o.SemanticConventionVersion = version)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var ddb = new AmazonDynamoDBClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(ddb, responseJson, "requestId", string.Empty, true);
#if NETFRAMEWORK
            Send(ddb, request);
#else
            await Send(ddb, request);
#endif
        }

        var activity = Assert.Single(exportedItems, a => a.DisplayName == "DynamoDB." + operation);

        var actual = activity.TagObjects
            .Where(t => t.Key.StartsWith("aws.dynamodb.", StringComparison.Ordinal))
            .ToDictionary(t => t.Key, t => t.Value);

        Assert.Equal(
            expected.Keys.OrderBy(k => k, StringComparer.Ordinal),
            actual.Keys.OrderBy(k => k, StringComparer.Ordinal));

        foreach (var pair in expected)
        {
            if (pair.Value is string[] expectedArray)
            {
                Assert.Equal(expectedArray, Assert.IsType<string[]>(actual[pair.Key]));
            }
            else
            {
                Assert.Equal(pair.Value, actual[pair.Key]);
            }
        }
    }

    [Fact]
#if NETFRAMEWORK
    public void EmitsSemanticConventionAttributesOnV1_28_0()
#else
    public Task EmitsSemanticConventionAttributesOnV1_28_0()
#endif
        => this.EmitsSemanticConventionAttributes(
            "Query",
            new QueryRequest { TableName = "T", Limit = 10 },
            "{}",
            new() { ["aws.dynamodb.table_names"] = TableNames, ["aws.dynamodb.limit"] = 10L },
            SemanticConventionVersion.V1_28_0);

    public void Dispose() =>
        RuntimePipelineCustomizerRegistry.Instance.Deregister(AWS.Implementation.AWSTracingPipelineCustomizer.UniqueName);

    private static Dictionary<string, object> ItemTags() => new()
    {
        ["aws.dynamodb.table_names"] = TableNames,
        ["aws.dynamodb.consumed_capacity"] = new[] { ExpectedConsumedCapacity },
        ["aws.dynamodb.item_collection_metrics"] = ExpectedItemCollectionMetrics,
    };

#if NETFRAMEWORK
    private static void Send(AmazonDynamoDBClient ddb, AmazonDynamoDBRequest request)
    {
        _ = request switch
        {
            QueryRequest r => (object)ddb.Query(r),
            ScanRequest r => ddb.Scan(r),
            GetItemRequest r => ddb.GetItem(r),
            BatchGetItemRequest r => ddb.BatchGetItem(r),
            BatchWriteItemRequest r => ddb.BatchWriteItem(r),
            PutItemRequest r => ddb.PutItem(r),
            UpdateItemRequest r => ddb.UpdateItem(r),
            DeleteItemRequest r => ddb.DeleteItem(r),
            CreateTableRequest r => ddb.CreateTable(r),
            UpdateTableRequest r => ddb.UpdateTable(r),
            ListTablesRequest r => ddb.ListTables(r),
            TransactGetItemsRequest r => ddb.TransactGetItems(r),
            _ => throw new NotSupportedException(request.GetType().Name),
        };
    }
#else
    private static Task Send(AmazonDynamoDBClient ddb, AmazonDynamoDBRequest request) => request switch
    {
        QueryRequest r => ddb.QueryAsync(r),
        ScanRequest r => ddb.ScanAsync(r),
        GetItemRequest r => ddb.GetItemAsync(r),
        BatchGetItemRequest r => ddb.BatchGetItemAsync(r),
        BatchWriteItemRequest r => ddb.BatchWriteItemAsync(r),
        PutItemRequest r => ddb.PutItemAsync(r),
        UpdateItemRequest r => ddb.UpdateItemAsync(r),
        DeleteItemRequest r => ddb.DeleteItemAsync(r),
        CreateTableRequest r => ddb.CreateTableAsync(r),
        UpdateTableRequest r => ddb.UpdateTableAsync(r),
        ListTablesRequest r => ddb.ListTablesAsync(r),
        TransactGetItemsRequest r => ddb.TransactGetItemsAsync(r),
        _ => throw new NotSupportedException(request.GetType().Name),
    };
#endif
}

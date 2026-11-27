// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceHelper
{
    // The semantic conventions for AWS SNS and AWS SQS describe sending a single message as a producer span
    // that preserves any existing message creation context, rather than a client span, since v1.44.0.
    private static readonly Version MessageSendProducerSpanVersion = new(1, 44, 0);

    public AWSServiceHelper(AWSSemanticConventions semanticConventions)
    {
        this.ParameterAttributeMap =
            semanticConventions
                .ParameterMappingBuilder
                .AddAttributeAWSDynamoTableName("TableName")
                .AddAttributeAWSDynamoTableName("RequestItems")
                .AddAttributeAWSDynamoAttributeDefinitions("AttributeDefinitions")
                .AddAttributeAWSDynamoAttributesToGet("AttributesToGet")
                .AddAttributeAWSDynamoConsistentRead("ConsistentRead")
                .AddAttributeAWSDynamoConsumedCapacity("ConsumedCapacity")
                .AddAttributeAWSDynamoCount("Count")
                .AddAttributeAWSDynamoExclusiveStartTable("ExclusiveStartTableName")
                .AddAttributeAWSDynamoGlobalSecondaryIndexUpdates("GlobalSecondaryIndexUpdates")
                .AddAttributeAWSDynamoGlobalSecondaryIndexes("GlobalSecondaryIndexes")
                .AddAttributeAWSDynamoIndexName("IndexName")
                .AddAttributeAWSDynamoItemCollectionMetrics("ItemCollectionMetrics")
                .AddAttributeAWSDynamoLimit("Limit")
                .AddAttributeAWSDynamoLocalSecondaryIndexes("LocalSecondaryIndexes")
                .AddAttributeAWSDynamoProjection("ProjectionExpression")
                .AddAttributeAWSDynamoProvisionedReadCapacity("ProvisionedThroughput.ReadCapacityUnits")
                .AddAttributeAWSDynamoProvisionedWriteCapacity("ProvisionedThroughput.WriteCapacityUnits")
                .AddAttributeAWSDynamoScanForward("ScanIndexForward")
                .AddAttributeAWSDynamoScannedCount("ScannedCount")
                .AddAttributeAWSDynamoSegment("Segment")
                .AddAttributeAWSDynamoSelect("Select")
                .AddAttributeAWSDynamoTableCount("TableNames")
                .AddAttributeAWSDynamoTotalSegments("TotalSegments")
                .AddAttributeAWSSNSTopicArn("TopicArn")
                .AddAttributeAWSSQSQueueUrl("QueueUrl")
                .AddAttributeGenAiModelId("ModelId")
                .AddAttributeAWSBedrockAgentId("AgentId")
                .AddAttributeAWSBedrockDataSourceId("DataSourceId")
                .AddAttributeAWSBedrockGuardrailId("GuardrailId")
                .AddAttributeAWSBedrockKnowledgeBaseId("KnowledgeBaseId")
                .AddAttributeAWSS3BucketName("BucketName")
                .AddAttributeAWSS3Key("Key")
                .AddAttributeMessageId("MessageId")
                .Build();

        this.ArrayValueAttributeNames = new(semanticConventions.GetArrayValueAttributeNames(), StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, List<string>> ServiceRequestParameterMap { get; } = new Dictionary<string, List<string>>(7)
    {
        { AWSServiceType.DynamoDbService, ["TableName"] },
        { AWSServiceType.S3Service, ["BucketName", "Key"] },
        { AWSServiceType.SNSService, ["TopicArn"] },
        { AWSServiceType.SQSService, ["QueueUrl"] },
        { AWSServiceType.BedrockAgentService, ["AgentId", "KnowledgeBaseId", "DataSourceId"] },
        { AWSServiceType.BedrockAgentRuntimeService, ["AgentId", "KnowledgeBaseId"] },
        { AWSServiceType.BedrockRuntimeService, ["ModelId"] },
    };

    internal static IReadOnlyDictionary<string, List<string>> ServiceResponseParameterMap { get; } = new Dictionary<string, List<string>>(4)
    {
        { AWSServiceType.BedrockService, ["GuardrailId"] },
        { AWSServiceType.BedrockAgentService, ["AgentId", "DataSourceId"] },
        { AWSServiceType.SNSService, ["MessageId", "TopicArn"] },
        { AWSServiceType.SQSService, ["MessageId", "QueueUrl"] },
    };

    // The DynamoDB request and response parameters recorded for each operation the semantic conventions define.
    // aws.dynamodb.table_names from TableName is recorded through ServiceRequestParameterMap.
    // See https://github.com/open-telemetry/semantic-conventions/blob/v1.45.0/docs/db/dynamodb.md
    internal static IReadOnlyDictionary<string, DynamoDbParameter[]> DynamoDbRequestParameterMap { get; } =
        BuildDynamoDbRequestParameterMap();

    internal static IReadOnlyDictionary<string, DynamoDbParameter[]> DynamoDbResponseParameterMap { get; } =
        BuildDynamoDbResponseParameterMap();

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> MessagingOperationTypeMap { get; } = new Dictionary<string, IReadOnlyDictionary<string, string>>(2)
    {
        // See https://github.com/open-telemetry/semantic-conventions/blob/v1.40.0/docs/registry/attributes/messaging.md#messaging-operation-type
        [AWSServiceType.SNSService] = new Dictionary<string, string>(2)
        {
            ["Publish"] = "send",
            ["PublishBatch"] = "send",
        },
        [AWSServiceType.SQSService] = new Dictionary<string, string>(3)
        {
            ["ReceiveMessage"] = "receive",
            ["SendMessage"] = "send",
            ["SendMessageBatch"] = "send",
        },
    };

    // for Bedrock Agent operations, we map each supported operation to one resource: Agent, DataSource, or KnowledgeBase
    internal static List<string> BedrockAgentAgentOps { get; } =
    [
        "CreateAgentActionGroup",
        "CreateAgentAlias",
        "DeleteAgentActionGroup",
        "DeleteAgentAlias",
        "DeleteAgent",
        "DeleteAgentVersion",
        "GetAgentActionGroup",
        "GetAgentAlias",
        "GetAgent",
        "GetAgentVersion",
        "ListAgentActionGroups",
        "ListAgentAliases",
        "ListAgentKnowledgeBases",
        "ListAgentVersions",
        "PrepareAgent",
        "UpdateAgentActionGroup",
        "UpdateAgentAlias",
        "UpdateAgent",
    ];

    internal static List<string> BedrockAgentKnowledgeBaseOps { get; } =
    [
        "AssociateAgentKnowledgeBase",
        "CreateDataSource",
        "DeleteKnowledgeBase",
        "DisassociateAgentKnowledgeBase",
        "GetAgentKnowledgeBase",
        "GetKnowledgeBase",
        "ListDataSources",
        "UpdateAgentKnowledgeBase",
    ];

    internal static List<string> BedrockAgentDataSourceOps { get; } =
    [
        "DeleteDataSource",
        "GetDataSource",
        "UpdateDataSource",
    ];

    internal static IReadOnlyDictionary<string, string> OperationNameToResourceMap { get; } = BuildOperationNameToResourceMap();

    internal IDictionary<string, string> ParameterAttributeMap { get; }

    internal HashSet<string> ArrayValueAttributeNames { get; }

    /// <summary>
    /// Gets whether the specified semantic conventions version describes sending a single SNS or SQS message as a producer span.
    /// </summary>
    /// <param name="semanticConventionVersion">The semantic conventions version.</param>
    /// <returns>
    /// <see langword="true"/> if the version describes producer spans for sending messages; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool UsesProducerSpansForMessageSend(SemanticConventionVersion semanticConventionVersion)
        => AWSSemanticConventions.GetVersion(semanticConventionVersion) >= MessageSendProducerSpanVersion;

    internal static string GetAWSServiceName(IRequestContext requestContext)
        => Utils.RemoveAmazonPrefixFromServiceName(requestContext.ServiceMetaData.ServiceId);

    internal static string GetAWSOperationName(IRequestContext requestContext)
    {
        var completeRequestName = requestContext.OriginalRequest.GetType().Name;
        var suffix = "Request";
        var operationName = Utils.RemoveSuffix(completeRequestName, suffix);
        return operationName;
    }

    private static Dictionary<string, DynamoDbParameter[]> BuildDynamoDbRequestParameterMap() =>
        new(8, StringComparer.Ordinal)
        {
            ["BatchGetItem"] = [new("RequestItems", DynamoDbValueKind.Keys)],
            ["BatchWriteItem"] = [new("RequestItems", DynamoDbValueKind.Keys)],
            ["CreateTable"] =
            [
                new("GlobalSecondaryIndexes", DynamoDbValueKind.StringArray),
                new("LocalSecondaryIndexes", DynamoDbValueKind.StringArray),
                new("ProvisionedThroughput.ReadCapacityUnits", DynamoDbValueKind.Double),
                new("ProvisionedThroughput.WriteCapacityUnits", DynamoDbValueKind.Double),
            ],
            ["GetItem"] =
            [
                new("ConsistentRead", DynamoDbValueKind.Bool),
                new("ProjectionExpression", DynamoDbValueKind.String),
            ],
            ["ListTables"] =
            [
                new("ExclusiveStartTableName", DynamoDbValueKind.String),
                new("Limit", DynamoDbValueKind.Long),
            ],
            ["Query"] =
            [
                new("AttributesToGet", DynamoDbValueKind.StringArray),
                new("ConsistentRead", DynamoDbValueKind.Bool),
                new("IndexName", DynamoDbValueKind.String),
                new("Limit", DynamoDbValueKind.Long),
                new("ProjectionExpression", DynamoDbValueKind.String),
                new("ScanIndexForward", DynamoDbValueKind.Bool),
                new("Select", DynamoDbValueKind.String),
            ],
            ["Scan"] =
            [
                new("AttributesToGet", DynamoDbValueKind.StringArray),
                new("ConsistentRead", DynamoDbValueKind.Bool),
                new("IndexName", DynamoDbValueKind.String),
                new("Limit", DynamoDbValueKind.Long),
                new("ProjectionExpression", DynamoDbValueKind.String),
                new("Segment", DynamoDbValueKind.Long),
                new("Select", DynamoDbValueKind.String),
                new("TotalSegments", DynamoDbValueKind.Long),
            ],
            ["UpdateTable"] =
            [
                new("AttributeDefinitions", DynamoDbValueKind.StringArray),
                new("GlobalSecondaryIndexUpdates", DynamoDbValueKind.StringArray),
                new("ProvisionedThroughput.ReadCapacityUnits", DynamoDbValueKind.Double),
                new("ProvisionedThroughput.WriteCapacityUnits", DynamoDbValueKind.Double),
            ],
        };

    private static Dictionary<string, DynamoDbParameter[]> BuildDynamoDbResponseParameterMap()
    {
        var consumedCapacity = new DynamoDbParameter("ConsumedCapacity", DynamoDbValueKind.StringArray);
        var itemCollectionMetrics = new DynamoDbParameter("ItemCollectionMetrics", DynamoDbValueKind.Json);

        return new Dictionary<string, DynamoDbParameter[]>(9, StringComparer.Ordinal)
        {
            ["BatchGetItem"] = [consumedCapacity],
            ["BatchWriteItem"] = [consumedCapacity, itemCollectionMetrics],
            ["DeleteItem"] = [consumedCapacity, itemCollectionMetrics],
            ["GetItem"] = [consumedCapacity],
            ["ListTables"] = [new("TableNames", DynamoDbValueKind.Count)],
            ["PutItem"] = [consumedCapacity, itemCollectionMetrics],
            ["Query"] = [consumedCapacity],
            ["Scan"] =
            [
                consumedCapacity,
                new("Count", DynamoDbValueKind.Long),
                new("ScannedCount", DynamoDbValueKind.Long),
            ],
            ["UpdateItem"] = [consumedCapacity, itemCollectionMetrics],
        };
    }

    private static Dictionary<string, string> BuildOperationNameToResourceMap()
    {
        var operationClassMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var op in BedrockAgentKnowledgeBaseOps)
        {
            operationClassMap[op] = "KnowledgeBaseId";
        }

        foreach (var op in BedrockAgentDataSourceOps)
        {
            operationClassMap[op] = "DataSourceId";
        }

        foreach (var op in BedrockAgentAgentOps)
        {
            operationClassMap[op] = "AgentId";
        }

        return operationClassMap;
    }
}

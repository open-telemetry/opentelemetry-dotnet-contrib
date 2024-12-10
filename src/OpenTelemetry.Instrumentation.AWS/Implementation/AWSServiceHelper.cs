// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceHelper
{
    internal static IReadOnlyDictionary<string, List<string>> ServiceRequestParameterMap = new Dictionary<string, List<string>>()
    {
        { AWSServiceType.DynamoDbService, ["TableName"] },
        { AWSServiceType.SQSService, ["QueueUrl"] },
        { AWSServiceType.BedrockAgentService, ["AgentId", "KnowledgeBaseId", "DataSourceId"] },
        { AWSServiceType.BedrockAgentRuntimeService, ["AgentId", "KnowledgeBaseId"] },
        { AWSServiceType.BedrockRuntimeService, ["ModelId"] },
    };

    internal static IReadOnlyDictionary<string, List<string>> ServiceResponseParameterMap = new Dictionary<string, List<string>>()
    {
        { AWSServiceType.BedrockService, ["GuardrailId"] },
        { AWSServiceType.BedrockAgentService, ["AgentId", "DataSourceId"] },
    };

    internal static IDictionary<string, string> ParameterAttributeMap =
        new Dictionary<string, string>()
            .AddAttributeAWSDynamoTableName("TableName")
            .AddAttributeAWSSQSQueueUrl("QueueUrl")
            .AddAttributeGenAiModelId("ModelId")
            .AddAttributeAWSBedrockAgentId("AgentId")
            .AddAttributeAWSBedrockDataSourceId("DataSourceId")
            .AddAttributeAWSBedrockGuardrailId("GuardrailId")
            .AddAttributeAWSBedrockKnowledgeBaseId("KnowledgeBaseId");

    // for Bedrock Agent operations, we map each supported operation to one resource: Agent, DataSource, or KnowledgeBase
    internal static List<string> BedrockAgentAgentOps =
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
        "UpdateAgent"
    ];

    internal static List<string> BedrockAgentKnowledgeBaseOps =
    [
        "AssociateAgentKnowledgeBase",
        "CreateDataSource",
        "DeleteKnowledgeBase",
        "DisassociateAgentKnowledgeBase",
        "GetAgentKnowledgeBase",
        "GetKnowledgeBase",
        "ListDataSources",
        "UpdateAgentKnowledgeBase"
    ];

    internal static List<string> BedrockAgentDataSourceOps =
    [
        "DeleteDataSource",
        "GetDataSource",
        "UpdateDataSource"
    ];

    internal static IReadOnlyDictionary<string, string> OperationNameToResourceMap()
    {
        var operationClassMap = new Dictionary<string, string>();

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

    internal static string GetAWSServiceName(IRequestContext requestContext)
        => Utils.RemoveAmazonPrefixFromServiceName(requestContext.ServiceMetaData.ServiceId);

    internal static string GetAWSOperationName(IRequestContext requestContext)
    {
        var completeRequestName = requestContext.OriginalRequest.GetType().Name;
        var suffix = "Request";
        var operationName = Utils.RemoveSuffix(completeRequestName, suffix);
        return operationName;
    }
}

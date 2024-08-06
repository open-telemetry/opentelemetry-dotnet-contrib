// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceHelper
{
    internal static IReadOnlyDictionary<string, List<string>> ServiceParameterMap = new Dictionary<string, List<string>>()
    {
        { AWSServiceType.DynamoDbService, new List<string> { "TableName" } },
        { AWSServiceType.SQSService, new List<string> { "QueueUrl" } },
        { AWSServiceType.BedrockRuntimeService, new List<string> { "ModelId" } },
        { AWSServiceType.BedrockAgentRuntimeService, new List<string> { "AgentId", "KnowledgeBaseId" } },
    };

    internal static IReadOnlyDictionary<string, string> ParameterAttributeMap = new Dictionary<string, string>()
    {
        { "TableName", AWSSemanticConventions.AttributeAWSDynamoTableName },
        { "QueueUrl", AWSSemanticConventions.AttributeAWSSQSQueueUrl },
        { "ModelId", AWSSemanticConventions.AttributeGenAiModelId },
        { "AgentId", AWSSemanticConventions.AttributeAWSBedrockAgentId },
        { "KnowledgeBaseId", AWSSemanticConventions.AttributeAWSBedrockKnowledgeBaseId },
        { "DataSourceId", AWSSemanticConventions.AttributeAWSBedrockDataSourceId },
    };

    // for Bedrock Agent operations, we map each supported operation to one resource: Agent, DataSource, or KnowledgeBase
    internal static List<string> BedrockAgentAgentOps = new List<string>
    {
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
    };

    internal static List<string> BedrockAgentKnowledgeBaseOps = new List<string>
    {
        "AssociateAgentKnowledgeBase",
        "CreateDataSource",
        "DeleteKnowledgeBase",
        "DisassociateAgentKnowledgeBase",
        "GetAgentKnowledgeBase",
        "GetKnowledgeBase",
        "ListDataSources",
        "UpdateAgentKnowledgeBase",
    };

    internal static List<string> BedrockAgentDataSourceOps = new List<string>
    {
        "DeleteDataSource",
        "GetDataSource",
        "UpdateDataSource",
    };

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
        string completeRequestName = requestContext.OriginalRequest.GetType().Name;
        string suffix = "Request";
        var operationName = Utils.RemoveSuffix(completeRequestName, suffix);
        return operationName;
    }
}

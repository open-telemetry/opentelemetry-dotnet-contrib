// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal static class AWSSemanticConventions
{
    public const string AttributeAWSServiceName = "aws.service";
    public const string AttributeAWSOperationName = "aws.operation";
    public const string AttributeAWSRegion = "aws.region";
    public const string AttributeAWSRequestId = "aws.requestId";

    public const string AttributeAWSDynamoTableName = "aws.table_name";
    public const string AttributeAWSSQSQueueUrl = "aws.queue_url";

    // AWS Bedrock service attributes not yet defined in semantic conventions
    public const string AttributeAWSBedrockAgentId = "aws.bedrock.agent.id";
    public const string AttributeAWSBedrockDataSourceId = "aws.bedrock.data_source.id";
    public const string AttributeAWSBedrockGuardrailId = "aws.bedrock.guardrail.id";
    public const string AttributeAWSBedrockKnowledgeBaseId = "aws.bedrock.knowledge_base.id";

    // should be global convention for Gen AI attributes
    public const string AttributeGenAiModelId = "gen_ai.request.model";
    public const string AttributeGenAiSystem = "gen_ai.system";

    public const string AttributeHttpStatusCode = "http.status_code";
    public const string AttributeHttpResponseContentLength = "http.response_content_length";

    public const string AttributeValueDynamoDb = "dynamodb";

    public const string AttributeValueRPCSystem = "rpc.system";
    public const string AttributeValueRPCService = "rpc.service";
    public const string AttributeValueRPCMethod = "rpc.method";
}

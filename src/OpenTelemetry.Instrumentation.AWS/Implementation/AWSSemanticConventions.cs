// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal static class AWSSemanticConventions
{
    public const string AttributeAWSDynamoTableName = AwsAttributes.AttributeAwsDynamodbTableNames; // todo - confirm in java;
    public const string AttributeAWSSQSQueueUrl = "aws.queue_url"; // todo - confirm in java;

    // AWS Bedrock service attributes not yet defined in semantic conventions
    public const string AttributeAWSBedrockAgentId = "aws.bedrock.agent.id";
    public const string AttributeAWSBedrockDataSourceId = "aws.bedrock.data_source.id";
    public const string AttributeAWSBedrockGuardrailId = "aws.bedrock.guardrail.id";
    public const string AttributeAWSBedrockKnowledgeBaseId = "aws.bedrock.knowledge_base.id";
    public const string AttributeAWSBedrock = "aws_bedrock";

    // should be global convention for Gen AI attributes
    public const string AttributeGenAiModelId = GenAiAttributes.AttributeGenAiRequestModel;
    public const string AttributeGenAiSystem = GenAiAttributes.AttributeGenAiSystem;

    //public const string AttributeHttpStatusCode = "http.status_code";

    public const string AttributeValueDynamoDb = DbAttributes.DbSystemValues.Dynamodb;
}

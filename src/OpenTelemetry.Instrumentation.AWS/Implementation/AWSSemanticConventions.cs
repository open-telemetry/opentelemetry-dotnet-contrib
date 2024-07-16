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
    public const string AttributeAWSSQSQueueName = "aws.sqs.queue_name";
    public const string AttributeAWSS3BucketName = "aws.s3.bucket";
    public const string AttributeAWSKinesisStreamName = "aws.kinesis.stream_name";

    public const string AttributeHttpStatusCode = "http.status_code";
    public const string AttributeHttpResponseContentLength = "http.response_content_length";

    public const string AttributeValueDynamoDb = "dynamodb";

    public const string AttributeValueRPCSystem = "rpc.system";
    public const string AttributeValueRPCService = "rpc.service";
    public const string AttributeValueRPCMethod = "rpc.method";
}

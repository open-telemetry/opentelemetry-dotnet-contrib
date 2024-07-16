// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceHelper
{
    internal static IReadOnlyDictionary<string, List<string>> ServiceParameterMap = new Dictionary<string, List<string>>()
    {
        { AWSServiceType.DynamoDbService, new List<string> { "TableName" } },
        { AWSServiceType.SQSService, new List<string> { "QueueUrl", "QueueName" } },
        { AWSServiceType.S3Service, new List<string> { "BucketName" } },
        { AWSServiceType.KinesisService, new List<string> { "StreamName" } },
    };

    internal static IReadOnlyDictionary<string, string> ParameterAttributeMap = new Dictionary<string, string>()
    {
        { "TableName", AWSSemanticConventions.AttributeAWSDynamoTableName },
        { "QueueUrl", AWSSemanticConventions.AttributeAWSSQSQueueUrl },
        { "QueueName", AWSSemanticConventions.AttributeAWSSQSQueueName },
        { "BucketName", AWSSemanticConventions.AttributeAWSS3BucketName },
        { "StreamName", AWSSemanticConventions.AttributeAWSKinesisStreamName },
    };

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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class AWSServiceHelper
{
    internal static IReadOnlyDictionary<string, string> ServiceParameterMap = new Dictionary<string, string>()
    {
        { AWSServiceType.DynamoDbService, "TableName" },
        { AWSServiceType.SQSService, "QueueUrl" },
    };

    internal static IReadOnlyDictionary<string, string> ParameterAttributeMap = new Dictionary<string, string>()
    {
        { "TableName", AWSSemanticConventions.AttributeAWSDynamoTableName },
        { "QueueUrl", AWSSemanticConventions.AttributeAWSSQSQueueUrl },
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

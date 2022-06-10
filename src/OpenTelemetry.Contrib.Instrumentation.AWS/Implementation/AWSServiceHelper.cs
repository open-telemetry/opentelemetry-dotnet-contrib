using Amazon.Runtime;
using System;
using System.Collections.Generic;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation
{
    internal class AWSServiceHelper
    {
        private const string DynamoDbService = "DynamoDBv2";
        private const string SQSService = "SQS";

        internal static IReadOnlyDictionary<string, string> ServiceParameterMap = new Dictionary<string, string>()
        {
            { DynamoDbService, "TableName" },
            { SQSService , "QueueUrl" },
        };

        internal static readonly IReadOnlyDictionary<string, string> ParameterAttributeMap = new Dictionary<string, string>()
        {
            { "TableName", AWSSemanticConventions.AttributeAWSDynamoTableName },
            { "QueueUrl", AWSSemanticConventions.AttributeAWSSQSQueueUrl },
        };

        internal static string GetAWSServiceName(IRequestContext requestContext) 
            => Utils.RemoveAmazonPrefixFromServiceName(requestContext.Request.ServiceName);

        internal static string GetAWSOperationName(IRequestContext requestContext)
        {
            string completeRequestName = requestContext.OriginalRequest.GetType().Name;
            string suffix = "Request";
            var operationName = Utils.RemoveSuffix(completeRequestName, suffix);
            return operationName;
        }

        internal static bool IsDbService(string service) 
            => DynamoDbService.Equals(service, StringComparison.OrdinalIgnoreCase);
    }
}

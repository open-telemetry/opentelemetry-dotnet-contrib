// <copyright file="AWSServiceHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
        => Utils.RemoveAmazonPrefixFromServiceName(requestContext.Request.ServiceName);

    internal static string GetAWSOperationName(IRequestContext requestContext)
    {
        string completeRequestName = requestContext.OriginalRequest.GetType().Name;
        string suffix = "Request";
        var operationName = Utils.RemoveSuffix(completeRequestName, suffix);
        return operationName;
    }
}

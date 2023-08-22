// <copyright file="AWSSemanticConventions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal static class AWSSemanticConventions
{
    public const string AttributeAWSServiceName = "aws.service";
    public const string AttributeAWSOperationName = "aws.operation";
    public const string AttributeAWSRegion = "aws.region";
    public const string AttributeAWSRequestId = "aws.requestId";

    public const string AttributeAWSDynamoTableName = "aws.table_name";
    public const string AttributeAWSSQSQueueUrl = "aws.queue_url";

    public const string AttributeHttpStatusCode = "http.status_code";
    public const string AttributeHttpResponseContentLength = "http.response_content_length";

    public const string AttributeValueDynamoDb = "dynamodb";
}

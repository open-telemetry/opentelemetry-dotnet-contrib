// <copyright file="SqsRequestContextHelper.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class SqsRequestContextHelper
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    internal static void AddAttributes(IRequestContext context, IReadOnlyDictionary<string, string> attributes)
    {
        var parameters = context.Request?.ParameterCollection;
        var originalRequest = context.OriginalRequest as SendMessageRequest;
        if (originalRequest?.MessageAttributes == null || parameters == null)
        {
            return;
        }

        if (attributes.Keys.Any(k => originalRequest.MessageAttributes.ContainsKey(k)))
        {
            // If at least one attribute is already present in the request then we skip the injection.
            return;
        }

        int attributesCount = originalRequest.MessageAttributes.Count;
        if (attributes.Count + attributesCount > MaxMessageAttributes)
        {
            // TODO: add logging (event source).
            return;
        }

        int nextAttributeIndex = attributesCount + 1;
        foreach (var param in attributes)
        {
            AddAttribute(parameters, originalRequest, param.Key, param.Value, nextAttributeIndex);
            nextAttributeIndex++;
        }
    }

    private static void AddAttribute(ParameterCollection parameters, SendMessageRequest originalRequest, string name, string value, int attributeIndex)
    {
        var prefix = "MessageAttribute." + attributeIndex;
        parameters.Add(prefix + ".Name", name);
        parameters.Add(prefix + ".Value.DataType", "String");
        parameters.Add(prefix + ".Value.StringValue", value);

        // Add injected attributes to the original request as well.
        // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
        originalRequest.MessageAttributes.Add(name, new MessageAttributeValue { DataType = "String", StringValue = value });
    }
}

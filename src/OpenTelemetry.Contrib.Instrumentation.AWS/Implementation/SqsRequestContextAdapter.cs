// <copyright file="SqsRequestContextAdapter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
internal class SqsRequestContextAdapter : IRequestContextAdapter
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    private readonly ParameterCollection? parameters;
    private readonly SendMessageRequest? originalRequest;

    public SqsRequestContextAdapter(IRequestContext context)
    {
        this.parameters = context.Request?.ParameterCollection;
        this.originalRequest = context.OriginalRequest as SendMessageRequest;
    }

    public bool CanInject => this.originalRequest?.MessageAttributes != null && this.parameters != null;

    public void AddAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        if (!this.CanInject)
        {
            return;
        }

        if (attributes.Keys.Any(k => this.ContainsAttribute(k)))
        {
            // If at least one attribute is already present in the request then we skip the injection.
            return;
        }

        int attributesCount = this.originalRequest?.MessageAttributes.Count ?? 0;
        if (attributes.Count + attributesCount > MaxMessageAttributes)
        {
            // TODO: add logging (event source).
            return;
        }

        int nextAttributeIndex = attributesCount + 1;
        foreach (var param in attributes)
        {
            this.AddAttribute(param.Key, param.Value, nextAttributeIndex);
            nextAttributeIndex++;
        }
    }

    private void AddAttribute(string name, string value, int attributeIndex)
    {
        if (!this.CanInject)
        {
            return;
        }

        var prefix = "MessageAttribute." + attributeIndex;
        this.parameters?.Add(prefix + ".Name", name);
        this.parameters?.Add(prefix + ".Value.DataType", "String");
        this.parameters?.Add(prefix + ".Value.StringValue", value);

        // Add injected attributes to the original request as well.
        // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
        this.originalRequest?.MessageAttributes.Add(name, new MessageAttributeValue { DataType = "String", StringValue = value });
    }

    private bool ContainsAttribute(string name) =>
        this.originalRequest?.MessageAttributes.ContainsKey(name) ?? false;
}

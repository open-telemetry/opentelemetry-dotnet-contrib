// <copyright file="AWSMessagingUtils.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;

internal static class AWSMessagingUtils
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    internal static void Inject(IRequestContextAdapter requestAdapter, PropagationContext propagationContext)
    {
        if (!requestAdapter.HasMessageBody ||
            !requestAdapter.HasOriginalRequest)
        {
            return;
        }

        var carrier = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, carrier, Setter);

        int attributesCount = requestAdapter.AttributesCount;
        if (carrier.Count + attributesCount > MaxMessageAttributes)
        {
            // TODO: Add logging (event source).
            return;
        }

        int nextAttributeIndex = attributesCount + 1;
        foreach (var param in carrier)
        {
            if (requestAdapter.ContainsAttribute(param.Key))
            {
                continue;
            }

            requestAdapter.AddAttribute(param.Key, param.Value, nextAttributeIndex);
            nextAttributeIndex++;

            // Add trace data to message attributes dictionary of the original request.
            // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
            requestAdapter.AddAttributeToOriginalRequest(param.Key, param.Value);
        }
    }

    private static void Setter(IDictionary<string, string> carrier, string name, string value) =>
        carrier[name] = value;
}

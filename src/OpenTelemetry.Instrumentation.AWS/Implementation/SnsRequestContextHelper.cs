// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService.Model;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class SnsRequestContextHelper
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    internal static void AddAttributes(IRequestContext context, IReadOnlyDictionary<string, string> attributes)
    {
        var parameters = context.Request?.ParameterCollection;
        var originalRequest = context.OriginalRequest as PublishRequest;
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

    private static void AddAttribute(ParameterCollection parameters, PublishRequest originalRequest, string name, string value, int attributeIndex)
    {
        var prefix = "MessageAttributes.entry." + attributeIndex;
        parameters.Add(prefix + ".Name", name);
        parameters.Add(prefix + ".Value.DataType", "String");
        parameters.Add(prefix + ".Value.StringValue", value);

        // Add injected attributes to the original request as well.
        // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
        originalRequest.MessageAttributes.Add(name, new MessageAttributeValue { DataType = "String", StringValue = value });
    }
}

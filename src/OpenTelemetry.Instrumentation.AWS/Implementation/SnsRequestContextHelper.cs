// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
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
        var originalRequest = context.OriginalRequest as PublishRequest;
        if (originalRequest?.MessageAttributes == null)
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

        foreach (var param in attributes)
        {
            originalRequest.MessageAttributes[param.Key] = new MessageAttributeValue { DataType = "String", StringValue = param.Value };
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
        if (context.OriginalRequest is not PublishRequest originalRequest)
        {
            return;
        }

        var existing = originalRequest.MessageAttributes;

        // Never write into the dictionary assigned by the application: it may be shared between
        // requests (or threads), and anything left in it would be sent again with later messages.
        var messageAttributes = new Dictionary<string, MessageAttributeValue>((existing?.Count ?? 0) + attributes.Count, existing?.Comparer);
        if (existing != null)
        {
            foreach (var attribute in existing)
            {
                // Values injected by an earlier send of the same request object are replaced with the current context.
                if (attribute.Value is not InjectedMessageAttributeValue)
                {
                    messageAttributes.Add(attribute.Key, attribute.Value);
                }
            }
        }

        if (CanInject(messageAttributes, attributes))
        {
            foreach (var param in attributes)
            {
                messageAttributes[param.Key] = new InjectedMessageAttributeValue { DataType = "String", StringValue = param.Value };
            }
        }
        else if (messageAttributes.Count == (existing?.Count ?? 0))
        {
            // Nothing to inject and nothing stale to remove.
            return;
        }

        originalRequest.MessageAttributes = messageAttributes;
    }

    private static bool CanInject(Dictionary<string, MessageAttributeValue> messageAttributes, IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in attributes.Keys)
        {
            if (messageAttributes.ContainsKey(key))
            {
                // If at least one attribute is already present in the request then we skip the injection.
                return false;
            }
        }

        // TODO: add logging (event source) when the limit is exceeded.
        return attributes.Count + messageAttributes.Count <= MaxMessageAttributes;
    }

    // Marks values added by the instrumentation so they can be told apart from application-supplied attributes.
    private sealed class InjectedMessageAttributeValue : MessageAttributeValue;
}

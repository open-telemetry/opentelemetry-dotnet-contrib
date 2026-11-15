// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class SqsRequestContextHelper
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    internal static bool IsInjectedByInstrumentation(MessageAttributeValue value)
        => value is InjectedMessageAttributeValue { IsUnchanged: true };

    internal static bool CanInjectContext(Dictionary<string, MessageAttributeValue>? existing, IReadOnlyDictionary<string, string> attributes)
    {
        // Gets whether AddAttributes would inject the specified propagation attributes into a request with the
        // existing message attributes. This is the case when there are attributes to inject, none of them has
        // the same name as an attribute supplied by the application, and the limit of message attributes is not exceeded.
        // Values injected by an earlier send of the same request are not counted, as they are replaced.
        if (attributes.Count == 0)
        {
            return false;
        }

        var retained = 0;

        if (existing != null)
        {
            foreach (var attribute in existing.Values)
            {
                if (!IsInjectedByInstrumentation(attribute))
                {
                    retained++;
                }
            }

            foreach (var key in attributes.Keys)
            {
                if (existing.TryGetValue(key, out var value) && !IsInjectedByInstrumentation(value))
                {
                    return false;
                }
            }
        }

        return retained + attributes.Count <= MaxMessageAttributes;
    }

    // Adds the specified propagation attributes to the request, replacing any added by an earlier send
    // of the same request. An empty set of attributes only removes attributes added by an earlier send.
    internal static void AddAttributes(IRequestContext context, IReadOnlyDictionary<string, string> attributes)
    {
        if (context.OriginalRequest is not SendMessageRequest originalRequest)
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
                // Values injected by an earlier send of the same request object are replaced with the current context,
                // unless the application has changed them since, in which case they are treated as the application's own.
                if (!IsInjectedByInstrumentation(attribute.Value))
                {
                    messageAttributes.Add(attribute.Key, attribute.Value);
                }
            }
        }

        if (attributes.Count > 0 && CanInject(messageAttributes, attributes))
        {
            foreach (var param in attributes)
            {
                messageAttributes[param.Key] = new InjectedMessageAttributeValue(param.Value);
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
    private sealed class InjectedMessageAttributeValue : MessageAttributeValue
    {
        private readonly string injectedValue;

        public InjectedMessageAttributeValue(string value)
        {
            this.DataType = "String";
            this.StringValue = value;
            this.injectedValue = value;
        }

        public bool IsUnchanged =>
            string.Equals(this.DataType, "String", StringComparison.Ordinal) &&
            string.Equals(this.StringValue, this.injectedValue, StringComparison.Ordinal);
    }
}

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal class AWSMessagingUtils
{
    // SNS attribute types: https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const string SnsAttributeTypeString = "String";
    private const string SnsAttributeTypeStringArray = "String.Array";
    private const string SnsMessageAttributes = "MessageAttributes";

    /// <summary>
    /// Gets or sets a value indicating whether the parent Activity should be set when SQS message batch is received.
    /// If option is set to true then the parent is set using the last received message otherwise the parent is not set at all.
    /// </summary>
    internal static bool SetParentFromMessageBatch { get; set; }

    internal static (PropagationContext ParentContext, IEnumerable<ActivityLink>? Links) ExtractParentContext(SQSEvent sqsEvent)
    {
        if (sqsEvent?.Records == null)
        {
            return (default, null);
        }

        // We choose the last message (record) as the carrier to set the parent.
        var parentRecord = SetParentFromMessageBatch ? sqsEvent.Records.LastOrDefault() : null;
        var parentContext = (parentRecord != null) ? ExtractParentContext(parentRecord) : default;

        var links = new List<ActivityLink>();
        foreach (var record in sqsEvent.Records)
        {
            var context = ReferenceEquals(record, parentRecord) ? parentContext : ExtractParentContext(record);
            if (context != default)
            {
                links.Add(new ActivityLink(context.ActivityContext));
            }
        }

        return (parentContext, links);
    }

    internal static PropagationContext ExtractParentContext(SQSEvent.SQSMessage sqsMessage)
    {
        if (sqsMessage?.MessageAttributes == null)
        {
            return default;
        }

        var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, sqsMessage.MessageAttributes, SqsMessageAttributeGetter);
        if (parentContext == default)
        {
            // SQS subscribed to SNS topic with raw delivery disabled case, i.e. SNS record serialized into SQS body.
            // https://docs.aws.amazon.com/sns/latest/dg/sns-large-payload-raw-message-delivery.html
            SNSEvent.SNSMessage? snsMessage = GetSnsMessage(sqsMessage);
            parentContext = ExtractParentContext(snsMessage);
        }

        return parentContext;
    }

    internal static PropagationContext ExtractParentContext(SNSEvent snsEvent)
    {
        // We assume there can be only a single SNS record (message) and records list is kept in the model consistency.
        // See https://aws.amazon.com/sns/faqs/#Reliability for details.
        var record = snsEvent?.Records?.LastOrDefault();
        return ExtractParentContext(record);
    }

    internal static PropagationContext ExtractParentContext(SNSEvent.SNSRecord? record)
    {
        return (record?.Sns?.MessageAttributes != null) ?
            Propagators.DefaultTextMapPropagator.Extract(default, record.Sns.MessageAttributes, SnsMessageAttributeGetter) :
            default;
    }

    internal static PropagationContext ExtractParentContext(SNSEvent.SNSMessage? message)
    {
        return (message?.MessageAttributes != null) ?
            Propagators.DefaultTextMapPropagator.Extract(default, message.MessageAttributes, SnsMessageAttributeGetter) :
            default;
    }

    private static IEnumerable<string>? SqsMessageAttributeGetter(IDictionary<string, SQSEvent.MessageAttribute> attributes, string attributeName)
    {
        if (!attributes.TryGetValue(attributeName, out var attribute))
        {
            return null;
        }

        return attribute?.StringValue != null ?
            new[] { attribute.StringValue } :
            attribute?.StringListValues;
    }

    private static IEnumerable<string>? SnsMessageAttributeGetter(IDictionary<string, SNSEvent.MessageAttribute> attributes, string attributeName)
    {
        if (!attributes.TryGetValue(attributeName, out var attribute))
        {
            return null;
        }

        switch (attribute?.Type)
        {
            case SnsAttributeTypeString when attribute.Value != null:
                return new[] { attribute.Value };
            case SnsAttributeTypeStringArray when attribute.Value != null:
                // Multiple values are stored as CSV (https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html).
                return attribute.Value.Split(',');
            default:
                return null;
        }
    }

    private static SNSEvent.SNSMessage? GetSnsMessage(SQSEvent.SQSMessage sqsMessage)
    {
        SNSEvent.SNSMessage? snsMessage = null;

        var body = sqsMessage.Body;
        if (body != null &&
            body.TrimStart().StartsWith("{", StringComparison.Ordinal) &&
            body.Contains(SnsMessageAttributes))
        {
            try
            {
                snsMessage = JsonConvert.DeserializeObject<SNSEvent.SNSMessage>(body);
            }
            catch (Exception)
            {
                // TODO: log exception.
                return null;
            }
        }

        return snsMessage;
    }
}

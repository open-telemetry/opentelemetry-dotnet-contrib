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

using Amazon.Runtime;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;

internal static class AWSMessagingUtils
{
    private static readonly AWSMessageAttributeHelper SqsAttributeHelper = new(new SqsMessageAttributeFormatter());
    private static readonly AWSMessageAttributeHelper SnsAttributeHelper = new(new SnsMessageAttributeFormatter());

    internal static void SqsMessageAttributeSetter(IRequestContext context, string name, string value)
    {
        var parameters = context.Request?.ParameterCollection;
        if (parameters == null ||
            !parameters.ContainsKey("MessageBody") ||
            context.OriginalRequest is not SQS::SendMessageRequest originalRequest)
        {
            return;
        }

        // Add trace data to parameters collection of the request.
        if (SqsAttributeHelper.TryAddParameter(parameters, name, value))
        {
            // Add trace data to message attributes dictionary of the original request.
            // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
            if (!originalRequest.MessageAttributes.ContainsKey(name))
            {
                originalRequest.MessageAttributes.Add(
                    name, new SQS::MessageAttributeValue
                    { DataType = "String", StringValue = value });
            }
        }
    }

    internal static void SnsMessageAttributeSetter(IRequestContext context, string name, string value)
    {
        var parameters = context.Request?.ParameterCollection;
        if (parameters == null ||
            !parameters.ContainsKey("Message") ||
            context.OriginalRequest is not SNS::PublishRequest originalRequest)
        {
            return;
        }

        if (SnsAttributeHelper.TryAddParameter(parameters, name, value))
        {
            // Add trace data to message attributes dictionary of the original request.
            // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
            if (!originalRequest.MessageAttributes.ContainsKey(name))
            {
                originalRequest.MessageAttributes.Add(
                    name, new SNS::MessageAttributeValue
                    { DataType = "String", StringValue = value });
            }
        }
    }
}

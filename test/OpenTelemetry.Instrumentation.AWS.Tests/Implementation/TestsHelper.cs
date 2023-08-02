// <copyright file="TestsHelper.cs" company="OpenTelemetry Authors">
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
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using Xunit;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

internal static class TestsHelper
{
    internal static Action<IRequestContext, IReadOnlyDictionary<string, string>>? CreateAddAttributesAction(string serviceType, IRequestContext context)
    {
        return serviceType switch
        {
            AWSServiceType.SQSService => SqsRequestContextHelper.AddAttributes,
            AWSServiceType.SNSService => SnsRequestContextHelper.AddAttributes,
            _ => throw new NotSupportedException($"Tests for service type {serviceType} not supported."),
        };
    }

    internal static AmazonWebServiceRequest CreateOriginalRequest(string serviceType, int attributesCount)
    {
        AmazonWebServiceRequest resultRequest;
        var sendRequest = new SQS::SendMessageRequest();
        var publishRequest = new SNS::PublishRequest();
        Action<int> addAttribute;

        switch (serviceType)
        {
            case AWSServiceType.SQSService:
                resultRequest = sendRequest;
                addAttribute = i => sendRequest.MessageAttributes.Add($"name{i}", new SQS::MessageAttributeValue { DataType = "String", StringValue = $"value{i}" });
                break;
            case AWSServiceType.SNSService:
                resultRequest = publishRequest;
                addAttribute = i => publishRequest.MessageAttributes.Add($"name{i}", new SNS::MessageAttributeValue { DataType = "String", StringValue = $"value{i}" });
                break;
            default:
                throw new NotSupportedException($"Tests for service type {serviceType} not supported.");
        }

        for (int i = 1; i <= attributesCount; i++)
        {
            addAttribute(i);
        }

        return resultRequest;
    }

    internal static void AddAttribute(this AmazonWebServiceRequest serviceRequest, string name, string value)
    {
        var sendRequest = serviceRequest as SQS::SendMessageRequest;
        var publishRequest = serviceRequest as SNS::PublishRequest;
        if (sendRequest != null)
        {
            sendRequest.MessageAttributes.Add(name, new SQS::MessageAttributeValue { DataType = "String", StringValue = value });
        }
        else if (publishRequest != null)
        {
            publishRequest.MessageAttributes.Add(name, new SNS::MessageAttributeValue { DataType = "String", StringValue = value });
        }
    }

    internal static void AddStringParameter(this ParameterCollection parameters, string serviceType, string name, string value, int index)
    {
        var prefix = $"{GetNamePrefix(serviceType)}.{index}";
        parameters.Add($"{prefix}.Name", name);
        parameters.Add($"{prefix}.Value.DataType", "String");
        parameters.Add($"{prefix}.Value.StringValue", value);
    }

    internal static void AddStringParameters(this ParameterCollection parameters, string serviceType, AmazonWebServiceRequest serviceRequest)
    {
        var sendRequest = serviceRequest as SQS::SendMessageRequest;
        var publishRequest = serviceRequest as SNS::PublishRequest;
        int index = 1;
        if (sendRequest != null)
        {
            foreach (var a in sendRequest.MessageAttributes)
            {
                AddStringParameter(parameters, serviceType, a.Key, a.Value.StringValue, index++);
            }
        }
        else if (publishRequest != null)
        {
            foreach (var a in publishRequest.MessageAttributes)
            {
                AddStringParameter(parameters, serviceType, a.Key, a.Value.StringValue, index++);
            }
        }
    }

    internal static void AssertStringParameters(string serviceType, List<KeyValuePair<string, string>> expectedParameters, ParameterCollection parameters)
    {
        Assert.Equal(expectedParameters.Count * 3, parameters.Count);

        for (int i = 0; i < expectedParameters.Count; i++)
        {
            var prefix = $"{GetNamePrefix(serviceType)}.{i + 1}";
            static string? Value(ParameterValue p) => (p as StringParameterValue)?.Value;

            Assert.True(parameters.ContainsKey($"{prefix}.Name"));
            Assert.Equal(expectedParameters[i].Key, Value(parameters[$"{prefix}.Name"]));

            Assert.True(parameters.ContainsKey($"{prefix}.Value.DataType"));
            Assert.Equal("String", Value(parameters[$"{prefix}.Value.DataType"]));

            Assert.True(parameters.ContainsKey($"{prefix}.Value.StringValue"));
            Assert.Equal(expectedParameters[i].Value, Value(parameters[$"{prefix}.Value.StringValue"]));
        }
    }

    private static string GetNamePrefix(string serviceType)
    {
        return serviceType switch
        {
            AWSServiceType.SQSService => "MessageAttribute",
            AWSServiceType.SNSService => "MessageAttributes.entry",
            _ => throw new NotSupportedException($"Tests for service type {serviceType} not supported."),
        };
    }
}

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
using OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Tests.Implementation;
internal static class TestsHelper
{
    internal static AWSMessageAttributeHelper CreateAttributeHelper(string serviceType)
    {
        return serviceType switch
        {
            AWSServiceType.SQSService => new(new SqsMessageAttributeFormatter()),
            AWSServiceType.SNSService => new(new SnsMessageAttributeFormatter()),
            _ => throw new NotSupportedException($"Tests for service type {serviceType} not supported."),
        };
    }

    internal static string GetNamePrefix(string serviceType)
    {
        return serviceType switch
        {
            AWSServiceType.SQSService => "MessageAttribute",
            AWSServiceType.SNSService => "MessageAttributes.entry",
            _ => throw new NotSupportedException($"Tests for service type {serviceType} not supported."),
        };
    }

    internal static void AddStringParameter(this ParameterCollection parameters, string name, string value, string namePrefix, int index)
    {
        var prefix = $"{namePrefix}.{index}";
        parameters.Add($"{prefix}.Name", name);
        parameters.Add($"{prefix}.Value.DataType", "String");
        parameters.Add($"{prefix}.Value.StringValue", value);
    }

    internal static void AddStringParameters(this ParameterCollection parameters, string namePrefix, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            AddStringParameter(parameters, $"name{i}", $"value{i}", namePrefix, i);
        }
    }

    internal static void AssertStringParameters(List<KeyValuePair<string, string>> expectedParameters, ParameterCollection actualParameters, string namePrefix)
    {
        Assert.Equal(expectedParameters.Count * 3, actualParameters.Count);

        for (int i = 0; i < expectedParameters.Count; i++)
        {
            var prefix = $"{namePrefix}.{i + 1}";
            static string Value(ParameterValue p) => (p as StringParameterValue).Value;

            Assert.True(actualParameters.ContainsKey($"{prefix}.Name"));
            Assert.Equal(expectedParameters[i].Key, Value(actualParameters[$"{prefix}.Name"]));

            Assert.True(actualParameters.ContainsKey($"{prefix}.Value.DataType"));
            Assert.Equal("String", Value(actualParameters[$"{prefix}.Value.DataType"]));

            Assert.True(actualParameters.ContainsKey($"{prefix}.Value.StringValue"));
            Assert.Equal(expectedParameters[i].Value, Value(actualParameters[$"{prefix}.Value.StringValue"]));
        }
    }
}

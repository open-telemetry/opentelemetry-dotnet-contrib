// <copyright file="SqsMessageAttributeHelperTests.cs" company="OpenTelemetry Authors">
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
using Amazon.Runtime.Internal;
using OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Tests.Implementation;

public class AWSMessageAttributeHelperTests
{
    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void TryAddParameter_CollectionSizeReachesLimit_ParameterNotAdded(string serviceType)
    {
        var helper = TestsHelper.CreateAttributeHelper(serviceType);
        var parameters = new ParameterCollection();
        parameters.AddStringParameters(TestsHelper.GetNamePrefix(serviceType), 10);

        var added = helper.TryAddParameter(parameters, "testName", "testValue");

        Assert.False(added, "Expected parameter not to be added.");
        Assert.Equal(30, parameters.Count);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void TryAddParameter_EmptyCollection_ParameterAdded(string serviceType)
    {
        var helper = TestsHelper.CreateAttributeHelper(serviceType);
        var parameters = new ParameterCollection();
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("testName", "testValue"),
        };

        var added = helper.TryAddParameter(parameters, "testName", "testValue");

        Assert.True(added, "Expected parameter to be added.");
        TestsHelper.AssertStringParameters(expectedParameters, parameters, TestsHelper.GetNamePrefix(serviceType));
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void TryAddParameter_CollectionWithSingleParameter_SecondParameterAdded(string serviceType)
    {
        var helper = TestsHelper.CreateAttributeHelper(serviceType);
        var parameters = new ParameterCollection();
        parameters.AddStringParameter("testNameA", "testValueA", TestsHelper.GetNamePrefix(serviceType), 1);

        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("testNameA", "testValueA"),
            new KeyValuePair<string, string>("testNameB", "testValueB"),
        };

        var added = helper.TryAddParameter(parameters, "testNameB", "testValueB");

        Assert.True(added, "Expected parameter to be added.");
        TestsHelper.AssertStringParameters(expectedParameters, parameters, TestsHelper.GetNamePrefix(serviceType));
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void TryAddParameter_ParameterPresentInCollection_ParameterNotAdded(string serviceType)
    {
        var helper = TestsHelper.CreateAttributeHelper(serviceType);
        var parameters = new ParameterCollection();
        parameters.AddStringParameter("testNameA", "testValueA", TestsHelper.GetNamePrefix(serviceType), 1);

        var added = helper.TryAddParameter(parameters, "testNameA", "testValueA");

        Assert.False(added, "Expected parameter not to be added.");
        Assert.Equal(3, parameters.Count);
    }
}

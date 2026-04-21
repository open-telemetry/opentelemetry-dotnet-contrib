// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

public class AWSLambdaUtilsTests
{
    [Fact]
    public void GetFunctionMemorySize_ConvertsMegabytesToBytesWithoutOverflow()
    {
        var context = new SampleLambdaContext
        {
            MemoryLimitInMB = 10240,
        };

        var memorySize = AWSLambdaUtils.GetFunctionMemorySize(context);

        Assert.Equal(10737418240L, memorySize);
    }
}

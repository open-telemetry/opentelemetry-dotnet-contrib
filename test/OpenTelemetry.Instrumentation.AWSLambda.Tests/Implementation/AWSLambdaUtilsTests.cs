// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AWSLambda.Implementation;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

public class AWSLambdaUtilsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("function", null)]
    [InlineData("arn:aws:lambda", null)]
    [InlineData("arn:aws:lambda:us-east-1:111111111111", "111111111111")]
    [InlineData("arn:aws:lambda:us-east-1::function:testfunction", "")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction", "111111111111")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:alias", "111111111111")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:1:extra", "111111111111")]
    public void GetAccountId_ReturnsFifthItemOfArn(string? functionArn, string? expected)
    {
        var actual = AWSLambdaUtils.GetAccountId(functionArn);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("function", "function")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111", "arn:aws:lambda:us-east-1:111111111111")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction", "arn:aws:lambda:us-east-1:111111111111:function:testfunction")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:alias", "arn:aws:lambda:us-east-1:111111111111:function:testfunction")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:1", "arn:aws:lambda:us-east-1:111111111111:function:testfunction")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:1:extra", "arn:aws:lambda:us-east-1:111111111111:function:testfunction")]
    [InlineData("arn:aws:lambda:us-east-1:111111111111:function:testfunction:", "arn:aws:lambda:us-east-1:111111111111:function:testfunction")]
    [InlineData(":::::::", "::::::")]
    public void GetFaasId_ExcludesVersionOrAliasFromArn(string? functionArn, string? expected)
    {
        var actual = AWSLambdaUtils.GetFaasId(functionArn);

        Assert.Equal(expected, actual);
    }

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

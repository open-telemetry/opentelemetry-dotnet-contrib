// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

public class AWSLambdaResourceBuilderExtensionsTests
{
    // Expected Semantic Conventions
    private const string AttributeCloudProvider = "cloud.provider";
    private const string AttributeCloudRegion = "cloud.region";
    private const string AttributeFaasName = "faas.name";
    private const string AttributeFaasVersion = "faas.version";
    private const string AttributeFaasInstance = "faas.instance";
    private const string AttributeFaasMaxMemory = "faas.max_memory";

    public AWSLambdaResourceBuilderExtensionsTests()
    {
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "testfunction");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", "latest");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", "128");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_STREAM_NAME",
            "2025/07/21/[$LATEST]7b176c212e954e62adfb9b5451cb5374");
    }

    [Fact]
    public void AssertAttributes()
    {
        var resourceBuilder = ResourceBuilder.CreateDefault();
        resourceBuilder.AddAWSLambdaDetector();

        var resource = resourceBuilder.Build();

        var resourceAttributes = resource.Attributes
            .ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("aws", resourceAttributes[AttributeCloudProvider]);
        Assert.Equal("us-east-1", resourceAttributes[AttributeCloudRegion]);
        Assert.Equal("testfunction", resourceAttributes[AttributeFaasName]);
        Assert.Equal("latest", resourceAttributes[AttributeFaasVersion]);
        Assert.Equal("2025/07/21/[$LATEST]7b176c212e954e62adfb9b5451cb5374",
            resourceAttributes[AttributeFaasInstance]);
        Assert.Equal(134217728L, resourceAttributes[AttributeFaasMaxMemory]);
    }

    [Fact]
    public void AssertArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => AWSLambdaResourceBuilderExtensions.AddAWSLambdaDetector(null!));
}

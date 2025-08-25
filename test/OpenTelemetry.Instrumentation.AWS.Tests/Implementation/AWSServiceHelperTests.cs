// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSServiceHelperTests
{
    private const string DefaultAwsRegion = "us-east-1";

    [Theory]
    [InlineData(DefaultAwsRegion)]
    [InlineData("eu-central-1")]
    [InlineData("sa-east-1")]
    [InlineData("us-gov-west-1")]
    public void ExtractCloudRegion_RegionEndpointIsSet_CloudRegionIsCorrect(string region)
    {
        var originalRequest = TestsHelper.CreateOriginalRequest(AWSServiceType.DynamoDbService);
        var requestContext = new TestRequestContext(originalRequest, new TestRequest())
        {
            ClientConfig = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            },
        };

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal(region, cloudRegion);
    }

    [Theory]
    [InlineData($"https://dynamodb.{DefaultAwsRegion}.amazonaws.com", DefaultAwsRegion)]
    [InlineData("https://dynamodb.eu-central-1.amazonaws.com", "eu-central-1")]
    [InlineData("https://dynamodb.us-gov-west-1.amazonaws.com", "us-gov-west-1")]
    [InlineData($"https://a1b2c3.execute-api.{DefaultAwsRegion}.amazonaws.com/default/mydynamodb", DefaultAwsRegion)]
    [InlineData("https://a1b2c3.execute-api.eu-central-1.amazonaws.com/default/", "eu-central-1")]
    [InlineData("https://a1b2c3.execute-api.us-gov-west-1.amazonaws.com/", "us-gov-west-1")]
    public void ExtractCloudRegion_ServiceUrlIsSet_CloudRegionIsCorrect(string serviceUrl, string expectedRegion)
    {
        var originalRequest = TestsHelper.CreateOriginalRequest(AWSServiceType.DynamoDbService);
        var requestContext = new TestRequestContext(originalRequest, new TestRequest())
        {
            ClientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl,
            },
        };

        var extractedRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal(expectedRegion, extractedRegion);
    }

    [Fact]
    public void ExtractCloudRegion_RegionIsMissingInServiceUrl_CloudRegionIsNull()
    {
        var originalRequest = TestsHelper.CreateOriginalRequest(AWSServiceType.DynamoDbService);
        var requestContext = new TestRequestContext(originalRequest, new TestRequest())
        {
            ClientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "https://mycompany.com/test",
            },
        };

        var extractedRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal(DefaultAwsRegion, extractedRegion);
    }

    [Theory]
    [InlineData("https://foo.bar.mycompany.com")]
    [InlineData("https://foo.bar.mycompany.com/test")]
    [InlineData("https://foo.baz.bar.mycomany.com/aaaa/bbbb")]
    public void ExtractCloudRegion_RegionNotFollowingEndpointPattern_CorrectPartOfUrlReturned(string serviceUrl)
    {
        var originalRequest = TestsHelper.CreateOriginalRequest(AWSServiceType.DynamoDbService);
        var requestContext = new TestRequestContext(originalRequest, new TestRequest())
        {
            ClientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl,
            },
        };

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal(DefaultAwsRegion, cloudRegion);
    }
}

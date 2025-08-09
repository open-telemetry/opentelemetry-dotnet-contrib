// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSServiceHelperTests
{
    [Fact]
    public void ExtractCloudRegion_RegionEndpointIsSet_CloudRegionIsCorrect()
    {
        var region = "eu-central-1";
        var clientConfig = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal(region, cloudRegion);
    }

    [Theory]
    [InlineData("https://dynamodb.eu-central-1.amazonaws.com")]
    [InlineData("https://a1b2c3.execute-api.eu-central-1.amazonaws.com/default/mydynamodb")]
    public void ExtractCloudRegion_ServiceUrlIsSet_CloudRegionIsCorrect(string serviceUrl)
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal("eu-central-1", cloudRegion);
    }

    [Fact]
    public void ExtractCloudRegion_RegionIsMissingInServiceUrl_CloudRegionIsNull()
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = "https://mycompany.com/test",
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal("us-east-1", cloudRegion); // AWS SDK default
    }

    [Theory]
    [InlineData("https://foo.bar.mycompany.com")]
    [InlineData("https://foo.bar.mycompany.com/test")]
    [InlineData("https://foo.baz.bar.mycomany.com/aaaa/bbbb")]
    public void ExtractCloudRegion_RegionNotFollowingEndpointPattern_CorrectPartOfUrlReturned(string serviceUrl)
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.ExtractCloudRegion(requestContext);

        Assert.Equal("us-east-1", cloudRegion); // AWS SDK default
    }
}

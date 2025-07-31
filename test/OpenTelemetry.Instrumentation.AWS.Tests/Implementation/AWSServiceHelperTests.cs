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
    public void GetDynamoDbCloudRegion_RegionEndpointIsSet_CloudRegionIsCorrect()
    {
        var region = "eu-central-1";
        var clientConfig = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext);

        Assert.Equal(region, cloudRegion);
    }

    [Theory]
    [InlineData("https://dynamodb.eu-central-1.amazonaws.com")]
    [InlineData("https://a1b2c3.execute-api.eu-central-1.amazonaws.com/default/mydynamodb")]
    public void GetDynamoDbCloudRegion_ServiceUrlIsSet_CloudRegionIsCorrect(string serviceUrl)
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext);

        Assert.Equal("eu-central-1", cloudRegion);
    }

    [Fact]
    public void GetDynamoDbCloudRegion_RegionIsMissingInServiceUrl_CloudRegionIsNull()
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = "https://mycompany.com/test",
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext);

        Assert.Null(cloudRegion);
    }

    [Fact]
    public void GetDynamoDbCloudRegion_RegionNotFollowingEndpointPattern_CloudRegionIsReturned()
    {
        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = "https://dynamodb.us-west-new.amazonaws.com",
        };
        var requestContext = new MockRequestContext(clientConfig);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext);

        Assert.Equal("us-west-new", cloudRegion);
    }
}

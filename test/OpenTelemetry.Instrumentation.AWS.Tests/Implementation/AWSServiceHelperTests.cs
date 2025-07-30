// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon;
using Amazon.Runtime;
using Moq;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSServiceHelperTests
{
    [Fact]
    public void GetDynamoDbCloudRegion_RegionEndpointIsSet_CloudRegionIsCorrect()
    {
        var region = "eu-central-1";
        var requestContext = new Mock<IRequestContext>();
        var clientConfig = new Mock<IClientConfig>();
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);
        clientConfig.Setup(c => c.RegionEndpoint).Returns(regionEndpoint);
        requestContext.Setup(c => c.ClientConfig).Returns(clientConfig.Object);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext.Object);

        Assert.Equal(region, cloudRegion);
    }

    [Theory]
    [InlineData("https://dynamodb.eu-central-1.amazonaws.com")]
    [InlineData("https://a1b2c3.execute-api.eu-central-1.amazonaws.com/default/mydynamodb")]
    public void GetDynamoDbCloudRegion_ServiceUrlIsSet_CloudRegionIsCorrect(string serviceUrl)
    {
        var requestContext = new Mock<IRequestContext>();
        var clientConfig = new Mock<IClientConfig>();
        clientConfig.Setup(c => c.ServiceURL).Returns(serviceUrl);
        requestContext.Setup(c => c.ClientConfig).Returns(clientConfig.Object);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext.Object);

        Assert.Equal("eu-central-1", cloudRegion);
    }

    [Fact]
    public void GetDynamoDbCloudRegion_RegionIsMissingInServiceUrl_CloudRegionIsNull()
    {
        var requestContext = new Mock<IRequestContext>();
        var clientConfig = new Mock<IClientConfig>();
        clientConfig.Setup(c => c.ServiceURL).Returns("https://mycompany.com/test");
        requestContext.Setup(c => c.ClientConfig).Returns(clientConfig.Object);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext.Object);

        Assert.Null(cloudRegion);
    }

    [Fact]
    public void GetDynamoDbCloudRegion_RegionNotFollowingEndpointPattern_CloudRegionIsReturned()
    {
        var requestContext = new Mock<IRequestContext>();
        var clientConfig = new Mock<IClientConfig>();
        clientConfig.Setup(c => c.ServiceURL).Returns("https://dynamodb.us-west-new.amazonaws.com");
        requestContext.Setup(c => c.ClientConfig).Returns(clientConfig.Object);

        var cloudRegion = AWSServiceHelper.GetDynamoDbCloudRegion(requestContext.Object);

        Assert.Equal("us-west-new", cloudRegion);
    }
}

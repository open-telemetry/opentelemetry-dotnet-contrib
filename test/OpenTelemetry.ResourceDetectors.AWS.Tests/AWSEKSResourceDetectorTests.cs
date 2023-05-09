// <copyright file="AWSEKSResourceDetectorTests.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK

using System.Linq;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests;

public class AWSEKSResourceDetectorTests
{
    private const string AWSEKSCredentialsPath = "SampleMetadataFiles/testekstoken";
    private const string AWSEKSMetadataFilePath = "SampleMetadataFiles/testcgroup";

    [Fact]
    public void TestDetect()
    {
        var eksResourceDetector = new AWSEKSResourceDetector();
        var resourceAttributes = eksResourceDetector.Detect();

        Assert.NotNull(resourceAttributes);
        Assert.Empty(resourceAttributes.Attributes);
    }

    [Fact]
    public void TestExtractResourceAttributes()
    {
        var clusterName = "Test cluster name";
        var containerId = "Test container id";

        var resourceAttributes = AWSEKSResourceDetector.ExtractResourceAttributes(clusterName, containerId).ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(4, resourceAttributes.Count);
        Assert.Equal("aws", resourceAttributes[AWSSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("aws_eks", resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("Test cluster name", resourceAttributes[AWSSemanticConventions.AttributeK8SClusterName]);
        Assert.Equal("Test container id", resourceAttributes[AWSSemanticConventions.AttributeContainerID]);
    }

    [Fact]
    public void TestExtractResourceAttributesWithEmptyClusterName()
    {
        var containerId = "Test container id";

        var resourceAttributes = AWSEKSResourceDetector.ExtractResourceAttributes(string.Empty, containerId).ToDictionary(x => x.Key, x => x.Value);

        // Validate the count of resourceAttributes -> Excluding cluster name, there will be only three resourceAttributes
        Assert.Equal(3, resourceAttributes.Count);
        Assert.Equal("aws", resourceAttributes[AWSSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("aws_eks", resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("Test container id", resourceAttributes[AWSSemanticConventions.AttributeContainerID]);
    }

    [Fact]
    public void TestExtractResourceAttributesWithEmptyContainerId()
    {
        var clusterName = "Test cluster name";

        var resourceAttributes = AWSEKSResourceDetector.ExtractResourceAttributes(clusterName, string.Empty).ToDictionary(x => x.Key, x => x.Value);

        // Validate the count of resourceAttributes -> Excluding container id, there will be only three resourceAttributes
        Assert.Equal(3, resourceAttributes.Count);
        Assert.Equal("aws", resourceAttributes[AWSSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("aws_eks", resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("Test cluster name", resourceAttributes[AWSSemanticConventions.AttributeK8SClusterName]);
    }

    [Fact]
    public void TestGetEKSCredentials()
    {
        var eksCredentials = AWSEKSResourceDetector.GetEKSCredentials(AWSEKSCredentialsPath);

        Assert.Equal("Bearer Test AWS EKS Token", eksCredentials);
    }

    [Fact]
    public void TestGetEKSContainerId()
    {
        var eksContainerId = AWSEKSResourceDetector.GetEKSContainerId(AWSEKSMetadataFilePath);

        Assert.Equal("a4d00c9dd675d67f866c786181419e1b44832d4696780152e61afd44a3e02856", eksContainerId);
    }

    [Fact]
    public void TestDeserializeResponse()
    {
        var awsEKSClusterInformation = "{\"kind\": \"ConfigMap\", \"apiVersion\": \"v1\", \"metadata\": {\"name\": \"cluster-info\", \"namespace\": \"amazon-cloudwatch\", \"selfLink\": \"/api/v1/namespaces/amazon-cloudwatch/configmaps/cluster-info\", \"uid\": \"0734438c-48f4-45c3-b06d-b6f16f7f0e1e\", \"resourceVersion\": \"25911\", \"creationTimestamp\": \"2021-07-23T18:41:56Z\", \"annotations\": {\"kubectl.kubernetes.io/last-applied-configuration\": \"{\\\"apiVersion\\\":\\\"v1\\\",\\\"data\\\":{\\\"cluster.name\\\":\\\"Test\\\",\\\"logs.region\\\":\\\"us-west-2\\\"},\\\"kind\\\":\\\"ConfigMap\\\",\\\"metadata\\\":{\\\"annotations\\\":{},\\\"name\\\":\\\"cluster-info\\\",\\\"namespace\\\":\\\"amazon-cloudwatch\\\"}}\\n\"}}, \"data\": {\"cluster.name\": \"Test\", \"logs.region\": \"us-west-2\"}}";

        var eksClusterInformation = AWSEKSResourceDetector.DeserializeResponse(awsEKSClusterInformation);

        Assert.NotNull(eksClusterInformation);
        Assert.NotNull(eksClusterInformation.Data);
        Assert.Equal("Test", eksClusterInformation.Data.ClusterName);
    }
}

#endif

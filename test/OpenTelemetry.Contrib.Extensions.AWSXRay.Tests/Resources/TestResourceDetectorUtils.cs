// <copyright file="TestResourceDetectorUtils.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Models;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests.Resources
{
    public class TestResourceDetectorUtils
    {
        [Fact]
        public void TestDeserializeAWSEC2IdentityDocument()
        {
            var awsEC2IdentityDocument = "{\"accountId\": \"123456789012\", \"architecture\": \"x86_64\", \"availabilityZone\": \"us-east-1a\", \"billingProducts\": null, \"devpayProductCodes\": null, \"marketplaceProductCodes\": null, \"imageId\": \"ami-12345678901234567\", \"instanceId\": \"i-12345678901234567\", \"instanceType\": \"t2.micro\", \"kernelId\": null, \"pendingTime\": \"2021-08-11T22:41:54Z\", \"privateIp\": \"123.456.789.123\", \"ramdiskId\": null, \"region\": \"us-east-1\", \"version\": \"2021-08-11\"}";

            var awsEC2IdentityDocumentModel = ResourceDetectorUtils.DeserializeFromString<AWSEC2IdentityDocumentModel>(awsEC2IdentityDocument);

            Assert.Equal("123456789012", awsEC2IdentityDocumentModel.AccountId);
            Assert.Equal("us-east-1a", awsEC2IdentityDocumentModel.AvailabilityZone);
            Assert.Equal("i-12345678901234567", awsEC2IdentityDocumentModel.InstanceId);
            Assert.Equal("t2.micro", awsEC2IdentityDocumentModel.InstanceType);
            Assert.Equal("us-east-1", awsEC2IdentityDocumentModel.Region);
        }

        [Fact]
        public void TestDeserializeAWSEKSClusterInformation()
        {
            var awsEKSClusterInformation = "{\"kind\": \"ConfigMap\", \"apiVersion\": \"v1\", \"metadata\": {\"name\": \"cluster-info\", \"namespace\": \"amazon-cloudwatch\", \"selfLink\": \"/api/v1/namespaces/amazon-cloudwatch/configmaps/cluster-info\", \"uid\": \"0734438c-48f4-45c3-b06d-b6f16f7f0e1e\", \"resourceVersion\": \"25911\", \"creationTimestamp\": \"2021-07-23T18:41:56Z\", \"annotations\": {\"kubectl.kubernetes.io/last-applied-configuration\": \"{\\\"apiVersion\\\":\\\"v1\\\",\\\"data\\\":{\\\"cluster.name\\\":\\\"Test\\\",\\\"logs.region\\\":\\\"us-west-2\\\"},\\\"kind\\\":\\\"ConfigMap\\\",\\\"metadata\\\":{\\\"annotations\\\":{},\\\"name\\\":\\\"cluster-info\\\",\\\"namespace\\\":\\\"amazon-cloudwatch\\\"}}\\n\"}}, \"data\": {\"cluster.name\": \"Test\", \"logs.region\": \"us-west-2\"}}";

            var awsEKSClusterInformationModel = ResourceDetectorUtils.DeserializeFromString<AWSEKSClusterInformationModel>(awsEKSClusterInformation);

            Assert.Equal("Test", awsEKSClusterInformationModel.Data.ClusterName);
        }

        [Fact]
        public void TestDeserializeAWSEBSMetadata()
        {
            string awsEBSMetadataFilePath = "Resources/SampleMetadataFiles/environment.conf";

            var awsEBSMetadata = ResourceDetectorUtils.DeserializeFromFile<AWSEBSMetadataModel>(awsEBSMetadataFilePath);

            Assert.Equal("1234567890", awsEBSMetadata.DeploymentId);
            Assert.Equal("Test AWS Elastic Beanstalk Environment Name", awsEBSMetadata.EnvironmentName);
            Assert.Equal("Test Version", awsEBSMetadata.VersionLabel);
        }
    }
}

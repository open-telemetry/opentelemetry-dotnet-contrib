// <copyright file="TestAWSEC2ResourceDetector.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests.Resources
{
    public class TestAWSEC2ResourceDetector
    {
        [Fact]
        public void TestDetect()
        {
            IEnumerable<KeyValuePair<string, object>> resourceAttributes;
            var ec2ResourceDetector = new AWSEC2ResourceDetector();
            resourceAttributes = ec2ResourceDetector.Detect().Attributes;
            Assert.Empty(resourceAttributes); // will be empty as it's not in ec2 environment
        }

        [Fact]
        public void TestExtractResourceAttributes()
        {
            var ec2ResourceDetector = new AWSEC2ResourceDetector();
            var sampleEC2IdentityDocumentModel = new SampleAWSEC2IdentityDocumentModel();
            var hostName = "Test host name";
            var resourceAttributes = ec2ResourceDetector.ExtractResourceAttributes(sampleEC2IdentityDocumentModel, hostName).ToDictionary(x => x.Key, x => x.Value);

            Assert.Equal("aws", resourceAttributes[AWSSemanticConventions.AttributeCloudProvider]);
            Assert.Equal("aws_ec2", resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform]);
            Assert.Equal("Test account id", resourceAttributes[AWSSemanticConventions.AttributeCloudAccountID]);
            Assert.Equal("Test availability zone", resourceAttributes[AWSSemanticConventions.AttributeCloudAvailableZone]);
            Assert.Equal("Test instance id", resourceAttributes[AWSSemanticConventions.AttributeHostID]);
            Assert.Equal("Test instance type", resourceAttributes[AWSSemanticConventions.AttributeHostType]);
            Assert.Equal("Test aws region", resourceAttributes[AWSSemanticConventions.AttributeCloudRegion]);
            Assert.Equal("Test host name", resourceAttributes[AWSSemanticConventions.AttributeHostName]);
        }

        [Fact]
        public void TestDeserializeResponse()
        {
            var ec2IdentityDocument = "{\"accountId\": \"123456789012\", \"architecture\": \"x86_64\", \"availabilityZone\": \"us-east-1a\", \"billingProducts\": null, \"devpayProductCodes\": null, \"marketplaceProductCodes\": null, \"imageId\": \"ami-12345678901234567\", \"instanceId\": \"i-12345678901234567\", \"instanceType\": \"t2.micro\", \"kernelId\": null, \"pendingTime\": \"2021-08-11T22:41:54Z\", \"privateIp\": \"123.456.789.123\", \"ramdiskId\": null, \"region\": \"us-east-1\", \"version\": \"2021-08-11\"}";

            var ec2ResourceDetector = new AWSEC2ResourceDetector();

            var ec2IdentityDocumentModel = ec2ResourceDetector.DeserializeResponse(ec2IdentityDocument);

            Assert.Equal("123456789012", ec2IdentityDocumentModel.AccountId);
            Assert.Equal("us-east-1a", ec2IdentityDocumentModel.AvailabilityZone);
            Assert.Equal("i-12345678901234567", ec2IdentityDocumentModel.InstanceId);
            Assert.Equal("t2.micro", ec2IdentityDocumentModel.InstanceType);
            Assert.Equal("us-east-1", ec2IdentityDocumentModel.Region);
        }
    }
}

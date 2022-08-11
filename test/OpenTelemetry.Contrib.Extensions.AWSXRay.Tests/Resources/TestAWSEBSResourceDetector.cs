// <copyright file="TestAWSEBSResourceDetector.cs" company="OpenTelemetry Authors">
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
    public class TestAWSEBSResourceDetector
    {
        private const string AWSEBSMetadataFilePath = "Resources/SampleMetadataFiles/environment.conf";

        [Fact]
        public void TestDetect()
        {
            IEnumerable<KeyValuePair<string, object>> resourceAttributes;
            var ebsResourceDetector = new AWSEBSResourceDetector();
            resourceAttributes = ebsResourceDetector.Detect().Attributes;
            Assert.Empty(resourceAttributes); // will be empty as it's not in ebs environment
        }

        [Fact]
        public void TestExtractResourceAttributes()
        {
            var ebsResourceDetector = new AWSEBSResourceDetector();
            var sampleModel = new SampleAWSEBSMetadataModel();

            var resourceAttributes = ebsResourceDetector.ExtractResourceAttributes(sampleModel).ToDictionary(x => x.Key, x => x.Value);

            Assert.Equal("aws", resourceAttributes[AWSSemanticConventions.AttributeCloudProvider]);
            Assert.Equal("aws_elastic_beanstalk", resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform]);
            Assert.Equal("aws_elastic_beanstalk", resourceAttributes[AWSSemanticConventions.AttributeServiceName]);
            Assert.Equal("Test environment name", resourceAttributes[AWSSemanticConventions.AttributeServiceNamespace]);
            Assert.Equal("Test ID", resourceAttributes[AWSSemanticConventions.AttributeServiceInstanceID]);
            Assert.Equal("Test version label", resourceAttributes[AWSSemanticConventions.AttributeServiceVersion]);
        }

        [Fact]
        public void TestGetEBSMetadata()
        {
            var ebsResourceDetector = new AWSEBSResourceDetector();
            var ebsMetadata = ebsResourceDetector.GetEBSMetadata(AWSEBSMetadataFilePath);

            Assert.Equal("1234567890", ebsMetadata.DeploymentId);
            Assert.Equal("Test AWS Elastic Beanstalk Environment Name", ebsMetadata.EnvironmentName);
            Assert.Equal("Test Version", ebsMetadata.VersionLabel);
        }
    }
}

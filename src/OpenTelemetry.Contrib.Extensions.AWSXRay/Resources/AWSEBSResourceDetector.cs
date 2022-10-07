// <copyright file="AWSEBSResourceDetector.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
#if NETSTANDARD
using System.Runtime.InteropServices;
#endif
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Models;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;

/// <summary>
/// Resource detector for application running in AWS ElasticBeanstalk environment.
/// </summary>
public class AWSEBSResourceDetector : IResourceDetector
{
    private const string AWSEBSMetadataWindowsFilePath = "C:\\Program Files\\Amazon\\XRay\\environment.conf";
    private const string AWSEBSMetadataLinuxFilePath = "/var/elasticbeanstalk/xray/environment.conf";

    /// <summary>
    /// Detector the required and optional resource attributes from AWS ElasticBeanstalk.
    /// </summary>
    /// <returns>List of key-value pairs of resource attributes.</returns>
    public IEnumerable<KeyValuePair<string, object>> Detect()
    {
        List<KeyValuePair<string, object>> resourceAttributes = null;

        try
        {
            string filePath = null;
#if NETSTANDARD
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filePath = AWSEBSMetadataWindowsFilePath;
            }
            else
            {
                filePath = AWSEBSMetadataLinuxFilePath;
            }
#else
            filePath = AWSEBSMetadataWindowsFilePath;
#endif

            var metadata = this.GetEBSMetadata(filePath);

            resourceAttributes = this.ExtractResourceAttributes(metadata);
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSEBSResourceDetector), ex);
        }

        return resourceAttributes;
    }

    internal List<KeyValuePair<string, object>> ExtractResourceAttributes(AWSEBSMetadataModel metadata)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudPlatform, "aws_elastic_beanstalk"),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeServiceName, "aws_elastic_beanstalk"),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeServiceNamespace, metadata.EnvironmentName),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeServiceInstanceID, metadata.DeploymentId),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeServiceVersion, metadata.VersionLabel),
        };

        return resourceAttributes;
    }

    internal AWSEBSMetadataModel GetEBSMetadata(string filePath)
    {
        return ResourceDetectorUtils.DeserializeFromFile<AWSEBSMetadataModel>(filePath);
    }
}

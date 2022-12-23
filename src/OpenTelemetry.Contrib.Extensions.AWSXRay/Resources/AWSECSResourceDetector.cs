// <copyright file="AWSECSResourceDetector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;

/// <summary>
/// Resource detector for application running in AWS ECS.
/// </summary>
public class AWSECSResourceDetector : IResourceDetector
{
    private const string AWSECSMetadataPath = "/proc/self/cgroup";
    private const string AWSECSMetadataURLKey = "ECS_CONTAINER_METADATA_URI";
    private const string AWSECSMetadataURLV4Key = "ECS_CONTAINER_METADATA_URI_V4";

    /// <summary>
    /// Detector the required and optional resource attributes from AWS ECS.
    /// </summary>
    /// <returns>List of key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        List<KeyValuePair<string, object>> resourceAttributes = null;

        if (!this.IsECSProcess())
        {
            return Resource.Empty;
        }

        try
        {
            var containerId = this.GetECSContainerId(AWSECSMetadataPath);

            resourceAttributes = this.ExtractResourceAttributes(containerId);
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), ex);
        }

        return new Resource(resourceAttributes);
    }

    internal List<KeyValuePair<string, object>> ExtractResourceAttributes(string containerId)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new(ResourceSemanticConventions.AttributeCloudProvider, "aws"),
            new(ResourceSemanticConventions.AttributeCloudPlatform, "aws_ecs"),
            new(ResourceSemanticConventions.AttributeContainerId, containerId),
        };

        return resourceAttributes;
    }

    internal string GetECSContainerId(string path)
    {
        string containerId = null;

        using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
        {
            while (!streamReader.EndOfStream)
            {
                var trimmedLine = streamReader.ReadLine().Trim();
                if (trimmedLine.Length > 64)
                {
                    containerId = trimmedLine.Substring(trimmedLine.Length - 64);
                    return containerId;
                }
            }
        }

        return containerId;
    }

    internal bool IsECSProcess()
    {
        return Environment.GetEnvironmentVariable(AWSECSMetadataURLKey) != null ||
               Environment.GetEnvironmentVariable(AWSECSMetadataURLV4Key) != null;
    }
}

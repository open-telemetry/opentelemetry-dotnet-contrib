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
using System.Net.Http;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

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
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        if (!IsECSProcess())
        {
            return Resource.Empty;
        }

        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeCloudPlatform, "aws_ecs"),
        };

        try
        {
            var containerId = GetECSContainerId(AWSECSMetadataPath);
            if (containerId != null)
            {
                resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeContainerID, containerId));
            }
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), ex);
        }

        try
        {
            resourceAttributes.AddRange(ExtractMetadataV4ResourceAttributes());
        }
        catch (Exception ex)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), ex);
        }

        return new Resource(resourceAttributes);
    }

    internal static List<KeyValuePair<string, object>> ExtractMetadataV4ResourceAttributes()
    {
        var metadataV4Url = Environment.GetEnvironmentVariable(AWSECSMetadataURLV4Key);
        if (metadataV4Url == null)
        {
            return new List<KeyValuePair<string, object>>();
        }

        using var httpClientHandler = new HttpClientHandler();
        var metadataV4ContainerResponse = ResourceDetectorUtils.SendOutRequest(metadataV4Url, "GET", null, httpClientHandler).Result;
        var metadataV4TaskResponse = ResourceDetectorUtils.SendOutRequest($"{metadataV4Url.TrimEnd('/')}/task", "GET", null, httpClientHandler).Result;

        var containerResponse = JObject.Parse(metadataV4ContainerResponse);
        var taskResponse = JObject.Parse(metadataV4TaskResponse);

        var containerArn = containerResponse.Value<string>("ContainerARN");
        if (containerArn == null)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException("The ECS Metadata V4 response did not contain the 'ContainerARN' field"));
            return new List<KeyValuePair<string, object>>();
        }

        var clusterArn = taskResponse.Value<string>("Cluster");
        if (clusterArn == null)
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException("The ECS Metadata V4 response did not contain the 'Cluster' field"));
            return new List<KeyValuePair<string, object>>();
        }

        if (!clusterArn.StartsWith("arn:", StringComparison.Ordinal))
        {
            var baseArn = containerArn.Substring(containerArn.LastIndexOf(":", StringComparison.Ordinal));
            clusterArn = $"{baseArn}:cluster/{clusterArn}";
        }

        var resourceAttributes = new List<KeyValuePair<string, object>>()
        {
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsContainerArn, containerArn),
            new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsClusterArn, clusterArn),
        };

        var launchType = taskResponse.Value<string>("LaunchType") switch
        {
            string type when string.Equals("ec2", type, StringComparison.OrdinalIgnoreCase) => AWSSemanticConventions.ValueEcsLaunchTypeEc2,
            string type when string.Equals("fargate", type, StringComparison.OrdinalIgnoreCase) => AWSSemanticConventions.ValueEcsLaunchTypeFargate,
            _ => null,
        };

        if (launchType != null)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsLaunchtype, launchType));
        }
        else
        {
            AWSXRayEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException($"The ECS Metadata V4 response contained the unrecognized launch type '{taskResponse["LaunchType"]}'"));
        }

        var taskArn = taskResponse.Value<string>("TaskARN");
        if (taskArn != null)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskArn, taskArn));
        }

        var family = taskResponse.Value<string>("Family");
        if (family != null)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskFamily, family));
        }

        var revision = taskResponse.Value<string>("Revision");
        if (revision != null)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskRevision, revision));
        }

        if (string.Equals("awslogs", containerResponse.Value<string>("LogDriver"), StringComparison.Ordinal))
        {
            JObject? logOptions = containerResponse.Value<JObject>("LogOptions");
            if (logOptions != null)
            {
                var regex = new Regex(@"arn:aws:ecs:([^:]+):([^:]+):.*");
                var match = regex.Match(containerArn);

                if (!match.Success)
                {
                    throw new ArgumentOutOfRangeException($"Cannot parse region and account from the container ARN '{containerArn}'");
                }

                var logsRegion = match.Groups[1];
                var logsAccount = match.Groups[2];

                var logGroupName = logOptions.Value<string>("awslogs-group");
                if (logGroupName != null)
                {
                    resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogGroupNames, new string[] { logGroupName }));
                    resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogGroupArns, new string[] { $"arn:aws:logs:{logsRegion}:{logsAccount}:log-group:{logGroupName}:*" }));

                    var logStreamName = logOptions.Value<string>("awslogs-stream");
                    if (logStreamName != null)
                    {
                        resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogStreamNames, new string[] { logStreamName }));
                        resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogStreamArns, new string[] { $"arn:aws:logs:{logsRegion}:{logsAccount}:log-group:{logGroupName}:log-stream:{logStreamName}" }));
                    }
                }
            }
        }

        return resourceAttributes;
    }

    internal static string? GetECSContainerId(string path)
    {
        string? containerId = null;

        using (var streamReader = ResourceDetectorUtils.GetStreamReader(path))
        {
            while (!streamReader.EndOfStream)
            {
                var trimmedLine = streamReader.ReadLine()!.Trim();
                if (trimmedLine.Length > 64)
                {
                    containerId = trimmedLine.Substring(trimmedLine.Length - 64);
                    return containerId;
                }
            }
        }

        return containerId;
    }

    internal static bool IsECSProcess()
    {
        return Environment.GetEnvironmentVariable(AWSECSMetadataURLKey) != null || Environment.GetEnvironmentVariable(AWSECSMetadataURLV4Key) != null;
    }
}

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

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.AWS;

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
            new(AWSSemanticConventions.AttributeCloudProvider, "aws"),
            new(AWSSemanticConventions.AttributeCloudPlatform, "aws_ecs"),
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
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), ex);
        }

        try
        {
            resourceAttributes.AddRange(ExtractMetadataV4ResourceAttributes());
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), ex);
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

        using var containerResponse = JsonDocument.Parse(metadataV4ContainerResponse);
        using var taskResponse = JsonDocument.Parse(metadataV4TaskResponse);

        if (!containerResponse.RootElement.TryGetProperty("ContainerARN", out var containerArnElement)
            || containerArnElement.GetString() is not string containerArn)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException("The ECS Metadata V4 response did not contain the 'ContainerARN' field"));
            return new List<KeyValuePair<string, object>>();
        }

        if (!taskResponse.RootElement.TryGetProperty("Cluster", out var clusterArnElement)
            || clusterArnElement.GetString() is not string clusterArn)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException("The ECS Metadata V4 response did not contain the 'Cluster' field"));
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

        if (!taskResponse.RootElement.TryGetProperty("LaunchType", out var launchTypeElement))
        {
            launchTypeElement = default;
        }

        var launchType = launchTypeElement switch
        {
            { ValueKind: JsonValueKind.String } when string.Equals("ec2", launchTypeElement.GetString(), StringComparison.OrdinalIgnoreCase) => AWSSemanticConventions.ValueEcsLaunchTypeEc2,
            { ValueKind: JsonValueKind.String } when string.Equals("fargate", launchTypeElement.GetString(), StringComparison.OrdinalIgnoreCase) => AWSSemanticConventions.ValueEcsLaunchTypeFargate,
            _ => null,
        };

        if (launchType != null)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsLaunchtype, launchType));
        }
        else
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException(nameof(AWSECSResourceDetector), new ArgumentException($"The ECS Metadata V4 response contained the unrecognized launch type '{launchTypeElement}'"));
        }

        if (taskResponse.RootElement.TryGetProperty("TaskARN", out var taskArnElement) && taskArnElement.ValueKind == JsonValueKind.String)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskArn, taskArnElement.GetString()!));
        }

        if (taskResponse.RootElement.TryGetProperty("Family", out var familyElement) && familyElement.ValueKind == JsonValueKind.String)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskFamily, familyElement.GetString()!));
        }

        if (taskResponse.RootElement.TryGetProperty("Revision", out var revisionElement) && revisionElement.ValueKind == JsonValueKind.String)
        {
            resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeEcsTaskRevision, revisionElement.GetString()!));
        }

        if (containerResponse.RootElement.TryGetProperty("LogDriver", out var logDriverElement)
            && logDriverElement.ValueKind == JsonValueKind.String
            && logDriverElement.ValueEquals("awslogs"))
        {
            if (containerResponse.RootElement.TryGetProperty("LogOptions", out var logOptionsElement))
            {
                var regex = new Regex(@"arn:aws:ecs:([^:]+):([^:]+):.*");
                var match = regex.Match(containerArn);

                if (!match.Success)
                {
                    throw new ArgumentOutOfRangeException($"Cannot parse region and account from the container ARN '{containerArn}'");
                }

                var logsRegion = match.Groups[1];
                var logsAccount = match.Groups[2];

                if (logOptionsElement.TryGetProperty("awslogs-group", out var logGroupElement) && logGroupElement.ValueKind == JsonValueKind.String)
                {
                    var logGroupName = logGroupElement.GetString()!;
                    resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogGroupNames, new[] { logGroupName }));
                    resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogGroupArns, new[] { $"arn:aws:logs:{logsRegion}:{logsAccount}:log-group:{logGroupName}:*" }));

                    if (logOptionsElement.TryGetProperty("awslogs-stream", out var logStreamElement) && logStreamElement.ValueKind == JsonValueKind.String)
                    {
                        var logStreamName = logStreamElement.GetString()!;
                        resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogStreamNames, new[] { logStreamName }));
                        resourceAttributes.Add(new KeyValuePair<string, object>(AWSSemanticConventions.AttributeLogStreamArns, new[] { $"arn:aws:logs:{logsRegion}:{logsAccount}:log-group:{logGroupName}:log-stream:{logStreamName}" }));
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
                var trimmedLine = streamReader.ReadLine()?.Trim();
                if (trimmedLine?.Length > 64)
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
#endif

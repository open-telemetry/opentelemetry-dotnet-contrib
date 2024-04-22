// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Google.Api.Gax;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.GCP;

/// <summary>
/// Resource detector for Google Cloud Platform (GCP).
/// </summary>
public sealed class GCPResourceDetector : IResourceDetector
{
    /// <inheritdoc/>
    public Resource Detect()
    {
        var platform = Platform.Instance();

        if (platform == null || platform.ProjectId == null)
        {
            return Resource.Empty;
        }

        List<KeyValuePair<string, object>> attributeList = new()
        {
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
        };

        switch (platform.Type)
        {
            case PlatformType.Gke:
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGkePlatformValue));
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudZone, platform.GkeDetails.Zone));
                attributeList.Add(new(ResourceSemanticConventions.AttributeK8sCluster, platform.GkeDetails.ClusterName));
                attributeList.Add(new(ResourceSemanticConventions.AttributeK8sNamespace, platform.GkeDetails.NamespaceId));
                attributeList.Add(new(ResourceSemanticConventions.AttributeK8sPod, platform.GkeDetails.HostName));
                break;
            case PlatformType.CloudRun:
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpCloudRunPlatformValue));
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudZone, platform.CloudRunDetails.Zone));
                break;
            case PlatformType.Gae:
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGaePlatformValue));
                break;
            case PlatformType.Gce:
            default:
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGcePlatformValue));
                attributeList.Add(new(ResourceSemanticConventions.AttributeHostId, platform.GceDetails.InstanceId));
                break;
        }

        return new Resource(attributeList);
    }
}

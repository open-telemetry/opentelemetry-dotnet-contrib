// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Google.Api.Gax;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Gcp;

/// <summary>
/// Resource detector for Google Cloud Platform (GCP).
/// </summary>
internal sealed class GcpResourceDetector : IResourceDetector
{
    private static readonly Version SemanticConventionsVersion = new(1, 44, 0);

    /// <inheritdoc/>
    public Resource Detect()
    {
        var platform = Platform.Instance();

        if (platform == null || platform.ProjectId == null)
        {
            return Resource.Empty;
        }

        var attributeList = platform.Type switch
        {
            PlatformType.Gke => ExtractGkeResourceAttributes(platform),
            PlatformType.CloudRun => ExtractCloudRunResourceAttributes(platform),
            PlatformType.Gae => ExtractGaeResourceAttributes(platform),
            PlatformType.Gce => ExtractGceResourceAttributes(platform),
            PlatformType.Unknown => ExtractGceResourceAttributes(platform),
            _ => ExtractGceResourceAttributes(platform),
        };

        return new Resource(attributeList, Internal.SchemaUrls.Get(SemanticConventionsVersion));
    }

    internal static List<KeyValuePair<string, object>> ExtractGkeResourceAttributes(Platform platform)
    {
        List<KeyValuePair<string, object>> attributeList =
        [
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
            new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGkePlatformValue),
            new(ResourceSemanticConventions.AttributeCloudAvailabilityZone, platform.GkeDetails.Zone),
            new(ResourceSemanticConventions.AttributeHostId, platform.GkeDetails.InstanceId),
            new(ResourceSemanticConventions.AttributeK8sCluster, platform.GkeDetails.ClusterName),
            new(ResourceSemanticConventions.AttributeK8sNamespace, platform.GkeDetails.NamespaceId),
            new(ResourceSemanticConventions.AttributeK8sPod, platform.GkeDetails.HostName)
        ];

        return attributeList;
    }

    internal static List<KeyValuePair<string, object>> ExtractCloudRunResourceAttributes(Platform platform)
    {
        List<KeyValuePair<string, object>> attributeList =
        [
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
            new(ResourceSemanticConventions.AttributeCloudAvailabilityZone, platform.CloudRunDetails.Zone),
            new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpCloudRunPlatformValue),
            new(ResourceSemanticConventions.AttributeCloudRegion, platform.CloudRunDetails.Region),
            new(ResourceSemanticConventions.AttributeFaasName, platform.CloudRunDetails.ServiceName),
            new(ResourceSemanticConventions.AttributeFaasVersion, platform.CloudRunDetails.RevisionName)
        ];

        // For faas.instance, use the GCE instance ID from the metadata service
        // This is the unique ID of the compute instance that the Cloud Run service is running on
        if (platform.GceDetails != null && !string.IsNullOrEmpty(platform.GceDetails.InstanceId))
        {
            attributeList.Add(new(ResourceSemanticConventions.AttributeFaasInstance, platform.GceDetails.InstanceId));
        }

        return attributeList;
    }

    internal static List<KeyValuePair<string, object>> ExtractGaeResourceAttributes(Platform platform)
    {
        List<KeyValuePair<string, object>> attributeList =
        [
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
            new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGaePlatformValue)
        ];
        return attributeList;
    }

    internal static List<KeyValuePair<string, object>> ExtractGceResourceAttributes(Platform platform)
    {
        List<KeyValuePair<string, object>> attributeList =
        [
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
            new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpGcePlatformValue),
            new(ResourceSemanticConventions.AttributeHostId, platform.GceDetails.InstanceId),
            new(ResourceSemanticConventions.AttributeCloudAvailabilityZone, platform.GceDetails.Location)
        ];

        AddGceInstanceAttributes(attributeList, platform.GceDetails.MetadataJson);

        return attributeList;
    }

    private static void AddGceInstanceAttributes(List<KeyValuePair<string, object>> attributeList, string? metadataJson)
    {
        if (metadataJson is not { Length: > 0 })
        {
            return;
        }

        try
        {
            using var metadata = JsonDocument.Parse(metadataJson);
            if (metadata.RootElement.ValueKind != JsonValueKind.Object
                || !metadata.RootElement.TryGetProperty("instance", out var instance)
                || instance.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (instance.TryGetProperty("machineType", out var machineTypeElement)
                && machineTypeElement.ValueKind == JsonValueKind.String
                && machineTypeElement.GetString() is { Length: > 0 } machineType)
            {
                attributeList.Add(new(ResourceSemanticConventions.AttributeHostType, machineType));
            }

            // The image is a resource path such as "projects/debian-cloud/global/images/debian-12-bookworm-v20240110".
            if (instance.TryGetProperty("image", out var imageElement)
                && imageElement.ValueKind == JsonValueKind.String
                && imageElement.GetString() is { Length: > 0 } image)
            {
                var imageName = image.Substring(image.LastIndexOf('/') + 1);
                if (imageName.Length > 0)
                {
                    attributeList.Add(new(ResourceSemanticConventions.AttributeHostImageName, imageName));
                }
            }
        }
        catch (JsonException ex)
        {
            GcpResourcesEventSource.Log.ResourceAttributesExtractException(nameof(GcpResourceDetector), ex);
        }
    }
}

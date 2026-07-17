// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Azure;

/// <summary>
/// Resource detector for Azure AppService environment.
/// </summary>
internal sealed class AppServiceResourceDetector : IResourceDetector
{
    internal static readonly IReadOnlyDictionary<string, string> AppServiceResourceAttributes = new Dictionary<string, string>
    {
        [ResourceSemanticConventions.AttributeCloudRegion] = ResourceAttributeConstants.AppServiceRegionNameEnvVar,
        [ResourceSemanticConventions.AttributeDeploymentEnvironmentName] = ResourceAttributeConstants.AppServiceSlotNameEnvVar,
        [ResourceSemanticConventions.AttributeHostId] = ResourceAttributeConstants.AppServiceHostNameEnvVar,
        [ResourceSemanticConventions.AttributeServiceInstance] = ResourceAttributeConstants.AppServiceInstanceIdEnvVar,
        [ResourceAttributeConstants.AzureAppServiceStamp] = ResourceAttributeConstants.AppServiceStampNameEnvVar,
    };

    /// <inheritdoc/>
    public Resource Detect()
    {
        try
        {
            var websiteSiteName = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceSiteNameEnvVar);

            if (websiteSiteName != null)
            {
                var attributeList = new List<KeyValuePair<string, object>>
                {
                    new(ResourceSemanticConventions.AttributeServiceName, websiteSiteName),
                    new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.AzureCloudProviderValue),
                    new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.AzureAppServicePlatformValue),
                };

                var azureResourceUri = GetAzureResourceURI(websiteSiteName);
                if (azureResourceUri != null)
                {
                    attributeList.Add(new(ResourceSemanticConventions.AttributeCloudResourceId, azureResourceUri));
                }

                foreach (var kvp in AppServiceResourceAttributes)
                {
                    var attributeValue = Environment.GetEnvironmentVariable(kvp.Value);
                    if (attributeValue != null)
                    {
                        attributeList.Add(new(kvp.Key, attributeValue));
                    }
                }

                return new Resource(
                    attributeList,
                    Internal.SchemaUrls.Get(AzureResourceBuilderExtensions.SemanticConventionsVersion));
            }
        }
        catch
        {
            // TODO: log exception.
        }

        return Resource.Empty;
    }

    private static string? GetAzureResourceURI(string websiteSiteName)
    {
        var websiteResourceGroup = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceResourceGroupEnvVar);
        var websiteOwnerName = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceOwnerNameEnvVar) ?? string.Empty;

#if NET
        var index = websiteOwnerName.IndexOf('+', StringComparison.Ordinal);
#else
        var index = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
#endif
        var subscriptionId = index > 0 ? websiteOwnerName.Substring(0, index) : websiteOwnerName;

        return string.IsNullOrEmpty(websiteResourceGroup) || string.IsNullOrEmpty(subscriptionId)
            ? null
            : $"/subscriptions/{subscriptionId}/resourceGroups/{websiteResourceGroup}/providers/Microsoft.Web/sites/{websiteSiteName}";
    }
}

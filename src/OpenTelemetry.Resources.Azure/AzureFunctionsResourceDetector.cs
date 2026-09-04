// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Azure;

/// <summary>
/// Resource detector for Azure Functions environments.
/// </summary>
internal sealed class AzureFunctionsResourceDetector : IResourceDetector
{
    internal static readonly IReadOnlyDictionary<string, string> AzureFunctionsResourceAttributes = new Dictionary<string, string>
    {
        [ResourceSemanticConventions.AttributeCloudRegion] = ResourceAttributeConstants.AppServiceRegionNameEnvVar,
        [ResourceSemanticConventions.AttributeDeploymentEnvironmentName] = ResourceAttributeConstants.AppServiceSlotNameEnvVar,
    };

    /// <inheritdoc/>
    public Resource Detect()
    {
        try
        {
            var attributeList = new List<KeyValuePair<string, object>>
            {
                new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.AzureCloudProviderValue),
                new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.AzureFunctionsPlatformValue),
            };

            var websiteSiteName = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceSiteNameEnvVar);
            if (!string.IsNullOrEmpty(websiteSiteName))
            {
                attributeList.Add(new(ResourceSemanticConventions.AttributeServiceName, websiteSiteName));
            }

            var websiteResourceGroup = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceResourceGroupEnvVar);
            if (!string.IsNullOrEmpty(websiteResourceGroup))
            {
                attributeList.Add(new(ResourceAttributeConstants.AzureResourceGroupName, websiteResourceGroup));
            }

            var websiteOwnerName = Environment.GetEnvironmentVariable(ResourceAttributeConstants.AppServiceOwnerNameEnvVar);
            var subscriptionId = AppServiceResourceDetector.GetSubscriptionId(websiteOwnerName);
            if (subscriptionId is { Length: > 0 })
            {
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudAccount, subscriptionId));
            }

            if (websiteSiteName is { Length: > 0 }
                && AppServiceResourceDetector.GetAzureResourceURI(websiteSiteName, websiteResourceGroup, subscriptionId) is { } azureResourceUri)
            {
                attributeList.Add(new(ResourceSemanticConventions.AttributeCloudResourceId, azureResourceUri));
            }

            foreach (var kvp in AzureFunctionsResourceAttributes)
            {
                var attributeValue = Environment.GetEnvironmentVariable(kvp.Value);
                if (!string.IsNullOrEmpty(attributeValue))
                {
                    attributeList.Add(new(kvp.Key, attributeValue));
                }
            }

            if (GetFunctionsInstanceId() is { } instanceId)
            {
                attributeList.Add(new(ResourceSemanticConventions.AttributeFaasInstance, instanceId));
            }

            return new Resource(
                attributeList,
                Internal.SchemaUrls.Get(AzureResourceBuilderExtensions.SemanticConventionsVersion));
        }
        catch (Exception ex)
        {
            AzureResourcesEventSource.Log.FailedToDetectAzureFunctionsResources(ex);
            return Resource.Empty;
        }
    }

    private static string? GetFunctionsInstanceId()
    {
        foreach (var environmentVariable in ResourceAttributeConstants.AzureFunctionsInstanceIdEnvVars)
        {
            if (Environment.GetEnvironmentVariable(environmentVariable) is { Length: > 0 } instanceId)
            {
                return instanceId;
            }
        }

        return null;
    }
}

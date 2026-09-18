// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Azure;

/// <summary>
/// Resource detector for Azure Functions environments.
/// </summary>
internal sealed class AzureFunctionsResourceDetector : IResourceDetector
{
#if NET
    internal static readonly FrozenDictionary<string, string> AzureFunctionsResourceAttributes = CreateAzureFunctionsResourceAttributes().ToFrozenDictionary();
#else
    internal static readonly Dictionary<string, string> AzureFunctionsResourceAttributes = CreateAzureFunctionsResourceAttributes();
#endif

    /// <inheritdoc/>
    public Resource Detect()
    {
        try
        {
            if (Environment.GetEnvironmentVariable(ResourceAttributeConstants.AzureFunctionsWorkerRuntimeEnvVar) == null)
            {
                return Resource.Empty;
            }

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

    private static Dictionary<string, string> CreateAzureFunctionsResourceAttributes()
    {
        return new Dictionary<string, string>
        {
            [ResourceSemanticConventions.AttributeCloudRegion] = ResourceAttributeConstants.AppServiceRegionNameEnvVar,
            [ResourceSemanticConventions.AttributeDeploymentEnvironmentName] = ResourceAttributeConstants.AppServiceSlotNameEnvVar,
        };
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

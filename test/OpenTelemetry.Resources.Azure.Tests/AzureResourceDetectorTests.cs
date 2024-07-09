// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Resources.Azure.Tests;

public class AzureResourceDetectorTests : IDisposable
{
    [Fact]
    public void AppServiceResourceDetectorReturnsResourceWithAttributes()
    {
        try
        {
            foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
            {
                if (kvp.Value == ResourceAttributeConstants.AppServiceSiteNameEnvVar)
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(kvp.Value, kvp.Key);
            }

            // Special case for service.name and resource uri attribute
            Environment.SetEnvironmentVariable(ResourceAttributeConstants.AppServiceSiteNameEnvVar, "sitename");
            Environment.SetEnvironmentVariable(ResourceAttributeConstants.AppServiceResourceGroupEnvVar, "testResourceGroup");
            Environment.SetEnvironmentVariable(ResourceAttributeConstants.AppServiceOwnerNameEnvVar, "testtestSubscriptionId+testResourceGroup-websiteOwnerName");
        }
        catch
        {
        }

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AppServiceResourceDetector()).Build();
        Assert.NotNull(resource);

        var expectedResourceUri = "/subscriptions/testtestSubscriptionId/resourceGroups/testResourceGroup/providers/Microsoft.Web/sites/sitename";
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeCloudResourceId, expectedResourceUri), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "sitename"), resource.Attributes);

        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
        }
    }

    [Fact]
    public void TestAzureVmResourceDetector()
    {
        AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse = () =>
        {
            return new AzureVmMetadataResponse()
            {
                // using values same as key for test.
                VmId = ResourceSemanticConventions.AttributeHostId,
                Location = ResourceSemanticConventions.AttributeCloudRegion,
                Name = ResourceSemanticConventions.AttributeHostName,
                OsType = ResourceSemanticConventions.AttributeOsType,
                ResourceId = ResourceSemanticConventions.AttributeCloudResourceId,
                Sku = ResourceAttributeConstants.AzureVmSku,
                Version = ResourceSemanticConventions.AttributeOsVersion,
                VmSize = ResourceSemanticConventions.AttributeHostType,
                VmScaleSetName = ResourceAttributeConstants.AzureVmScaleSetName,
            };
        };

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AzureVMResourceDetector()).Build();
        Assert.NotNull(resource);

        foreach (var field in AzureVMResourceDetector.ExpectedAzureAmsFields)
        {
            KeyValuePair<string, object> expectedValue;
            if (field == ResourceSemanticConventions.AttributeServiceInstance)
            {
                expectedValue = new KeyValuePair<string, object>(field, ResourceSemanticConventions.AttributeHostId);
            }
            else if (field == ResourceSemanticConventions.AttributeCloudPlatform)
            {
                expectedValue = new KeyValuePair<string, object>(field, ResourceAttributeConstants.AzureVmCloudPlatformValue);
            }
            else if (field == ResourceSemanticConventions.AttributeCloudProvider)
            {
                expectedValue = new KeyValuePair<string, object>(field, ResourceAttributeConstants.AzureCloudProviderValue);
            }
            else
            {
                expectedValue = new KeyValuePair<string, object>(field, field);
            }

            Assert.Contains(expectedValue, resource.Attributes);
        }
    }

    [Fact]
    public void AzureContainerAppsResourceDetectorReturnsResourceWithAttributes()
    {
        try
        {
            foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerResourceAttributes)
            {
                Environment.SetEnvironmentVariable(kvp.Value, kvp.Key);
            }

            Environment.SetEnvironmentVariable(ResourceAttributeConstants.AzureContainerAppsNameEnvVar, "containerAppName");
        }
        catch
        {
        }

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AzureContainerAppsResourceDetector()).Build();
        Assert.NotNull(resource);

        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "containerAppName"), resource.Attributes);

        foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerResourceAttributes)
        {
            Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            Environment.SetEnvironmentVariable(kvp.Value, null);
        }

        foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerResourceAttributes)
        {
            Environment.SetEnvironmentVariable(kvp.Value, null);
        }
    }
}

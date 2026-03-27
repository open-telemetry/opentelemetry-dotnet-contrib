// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Resources.Azure.Tests;

public class AzureResourceDetectorTests
{
    [Fact]
    public void AppServiceResourceDetectorReturnsResourceWithAttributes()
    {
        var environment = new Dictionary<string, string?>();

        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            if (kvp.Value == ResourceAttributeConstants.AppServiceSiteNameEnvVar)
            {
                continue;
            }

            environment[kvp.Value] = kvp.Key;
        }

        // Special case for service.name and resource uri attribute
        environment[ResourceAttributeConstants.AppServiceSiteNameEnvVar] = "sitename";
        environment[ResourceAttributeConstants.AppServiceResourceGroupEnvVar] = "testResourceGroup";
        environment[ResourceAttributeConstants.AppServiceOwnerNameEnvVar] = "testtestSubscriptionId+testResourceGroup-websiteOwnerName";

        using (EnvironmentVariableScope.Create(environment))
        {
            var resource = ResourceBuilder.CreateEmpty().AddAzureAppServiceDetector().Build();
            Assert.NotNull(resource);

            var expectedResourceUri = "/subscriptions/testtestSubscriptionId/resourceGroups/testResourceGroup/providers/Microsoft.Web/sites/sitename";
            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeCloudResourceId, expectedResourceUri), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "sitename"), resource.Attributes);

            foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
            }
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
                Version = ResourceSemanticConventions.AttributeOsVersion,
                VmSize = ResourceSemanticConventions.AttributeHostType,
                VmScaleSetName = ResourceAttributeConstants.AzureVmScaleSetName,
            };
        };

        var resource = ResourceBuilder.CreateEmpty().AddAzureVMDetector().Build();
        Assert.NotNull(resource);

        foreach (var field in AzureVMResourceDetector.ExpectedAzureAmsFields)
        {
            var expectedValue = field switch
            {
                ResourceSemanticConventions.AttributeServiceInstance => new KeyValuePair<string, object>(field, ResourceSemanticConventions.AttributeHostId),
                ResourceSemanticConventions.AttributeCloudPlatform => new KeyValuePair<string, object>(field, ResourceAttributeConstants.AzureVmCloudPlatformValue),
                ResourceSemanticConventions.AttributeCloudProvider => new KeyValuePair<string, object>(field, ResourceAttributeConstants.AzureCloudProviderValue),
                _ => new KeyValuePair<string, object>(field, field),
            };

            Assert.Contains(expectedValue, resource.Attributes);
        }
    }

    [Fact]
    public void AzureContainerAppsResourceDetectorReturnsResourceWithAttributes()
    {
        var environment = new Dictionary<string, string?>();

        foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerAppResourceAttributes)
        {
            environment[kvp.Value] = kvp.Key;
        }

        environment[ResourceAttributeConstants.AzureContainerAppsNameEnvVar] = "containerAppName";

        using (EnvironmentVariableScope.Create(environment))
        {
            var resource = ResourceBuilder.CreateEmpty().AddAzureContainerAppsDetector().Build();
            Assert.NotNull(resource);

            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "containerAppName"), resource.Attributes);

            foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerAppResourceAttributes)
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
            }
        }
    }

    [Fact]
    public void AzureContainerAppsJobResourceDetectorReturnsResourceWithAttributes()
    {
        var environment = new Dictionary<string, string?>();

        foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerAppJobResourceAttributes)
        {
            environment[kvp.Value] = kvp.Key;
        }

        environment[ResourceAttributeConstants.AzureContainerAppJobNameEnvVar] = "containerAppJobName";

        using (EnvironmentVariableScope.Create(environment))
        {
            var resource = ResourceBuilder.CreateEmpty().AddAzureContainerAppsDetector().Build();
            Assert.NotNull(resource);

            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "containerAppJobName"), resource.Attributes);

            foreach (var kvp in AzureContainerAppsResourceDetector.AzureContainerAppJobResourceAttributes)
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, kvp.Key), resource.Attributes);
            }
        }
    }

    [Fact]
    public void AppServiceDetectorServiceNameOverridesEarlierCustomName()
    {
        var environment = new Dictionary<string, string?>
        {
            [ResourceAttributeConstants.AppServiceSiteNameEnvVar] = "my-app-service",
        };

        using (EnvironmentVariableScope.Create(environment))
        {
            var resource = ResourceBuilder
                .CreateEmpty()
                .AddAttributes([new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "custom-service")])
                .AddAzureAppServiceDetector()
                .Build();

            Assert.NotNull(resource);

            // Detector is applied after AddAttributes, so detector value wins.
            Assert.Contains(
                new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-app-service"),
                resource.Attributes);
        }
    }

    [Fact]
    public void CustomServiceNameAfterAppServiceDetectorOverridesDetectorValue()
    {
        var environment = new Dictionary<string, string?>
        {
            [ResourceAttributeConstants.AppServiceSiteNameEnvVar] = "my-app-service",
        };

        using (EnvironmentVariableScope.Create(environment))
        {
            var resource = ResourceBuilder
                .CreateEmpty()
                .AddAzureAppServiceDetector()
                .AddAttributes([new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "custom-service")])
                .Build();

            Assert.NotNull(resource);

            // AddAttributes is applied after the detector, so the custom value wins.
            Assert.Contains(
                new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "custom-service"),
                resource.Attributes);
        }
    }
}

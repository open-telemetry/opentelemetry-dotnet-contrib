// <copyright file="AzureResourceDetectorTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Azure.Tests;

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

    public void Dispose()
    {
        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            Environment.SetEnvironmentVariable(kvp.Value, null);
        }
    }
}

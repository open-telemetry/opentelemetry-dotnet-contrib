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

            // Special case for service.name and appSrv_SiteName attribute
            Environment.SetEnvironmentVariable(ResourceAttributeConstants.AppServiceSiteNameEnvVar, "ServiceName");
        }
        catch
        {
        }

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AppServiceResourceDetector()).Build();
        Assert.NotNull(resource);

        foreach (var kvp in AppServiceResourceDetector.AppServiceResourceAttributes)
        {
            if (kvp.Value == ResourceAttributeConstants.AppServiceSiteNameEnvVar)
            {
                Assert.Contains(new KeyValuePair<string, object>(kvp.Key, "ServiceName"), resource.Attributes);
                continue;
            }

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
                VmId = ResourceAttributeConstants.AzureVmId,
                Location = ResourceAttributeConstants.AzureVmLocation,
                Name = ResourceAttributeConstants.AzureVmName,
                OsType = ResourceAttributeConstants.AzureVmOsType,
                ResourceGroupName = ResourceAttributeConstants.AzureVmResourceGroup,
                ResourceId = ResourceAttributeConstants.AzureVmResourceId,
                Sku = ResourceAttributeConstants.AzureVmSku,
                Version = ResourceAttributeConstants.AzureVmVersion,
                VmSize = ResourceAttributeConstants.AzureVmSize,
                VmScaleSetName = ResourceAttributeConstants.AzureVmScaleSetName,
                SubscriptionId = ResourceAttributeConstants.AzureVmSubscriptionId,
            };
        };

        var resource = ResourceBuilder.CreateEmpty().AddDetector(new AzureVMResourceDetector()).Build();
        Assert.NotNull(resource);
        foreach (var field in AzureVMResourceDetector.ExpectedAzureAmsFields)
        {
            if (field == ResourceSemanticConventions.AttributeServiceInstance)
            {
                Assert.Contains(new KeyValuePair<string, object>(field, ResourceAttributeConstants.AzureVmId), resource.Attributes);
                continue;
            }

            Assert.Contains(new KeyValuePair<string, object>(field, field), resource.Attributes);
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

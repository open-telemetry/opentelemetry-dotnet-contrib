// <copyright file="AzureVMResourceDetector.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.Azure;

/// <summary>
/// Resource detector for Azure VM environment.
/// </summary>
public sealed class AzureVMResourceDetector : IResourceDetector
{
    internal static readonly IReadOnlyCollection<string> ExpectedAzureAmsFields = new string[]
    {
        "azInst_location",
        "azInst_name",
        "azInst_osType",
        "azInst_resourceGroupName",
        "azInst_resourceId",
        "azInst_sku",
        "azInst_subscriptionId",
        "azInst_version",
        "azInst_vmId",
        "azInst_vmSize",
        "azInst_vmScaleSetName",
        ResourceSemanticConventions.AttributeServiceInstance,
    };

    /// <inheritdoc/>
    public Resource Detect()
    {
        List<KeyValuePair<string, object>>? attributeList = null;
        try
        {
            var vmMetaDataResponse = AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse();
            if (vmMetaDataResponse != null)
            {
                attributeList = new List<KeyValuePair<string, object>>(ExpectedAzureAmsFields.Count);
                foreach (var field in ExpectedAzureAmsFields)
                {
                    attributeList.Add(new KeyValuePair<string, object>(field, vmMetaDataResponse.GetValueForField(field)));
                }
            }
        }
        catch
        {
            // TODO: log exception.
            return Resource.Empty;
        }

        return attributeList == null ? Resource.Empty : new Resource(attributeList);
    }
}

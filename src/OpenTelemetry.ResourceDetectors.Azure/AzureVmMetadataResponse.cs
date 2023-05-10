// <copyright file="AzureVmMetadataResponse.cs" company="OpenTelemetry Authors">
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

using System.Text.Json.Serialization;
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.Azure;
internal sealed class AzureVmMetadataResponse
{
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("osType")]
    public string? OsType { get; set; }

    [JsonPropertyName("resourceGroupName")]
    public string? ResourceGroupName { get; set; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("vmId")]
    public string? VmId { get; set; }

    [JsonPropertyName("vmScaleSetName")]
    public string? VmScaleSetName { get; set; }

    [JsonPropertyName("vmSize")]
    public string? VmSize { get; set; }

    internal string GetValueForField(string fieldName)
    {
        string? amsValue = null;
        switch (fieldName)
        {
            case "azInst_osType":
                amsValue = this.OsType;
                break;
            case "azInst_location":
                amsValue = this.Location;
                break;
            case "azInst_name":
                amsValue = this.Name;
                break;
            case "azInst_sku":
                amsValue = this.Sku;
                break;
            case "azInst_version":
                amsValue = this.Version;
                break;
            case "azInst_vmId":
            case ResourceSemanticConventions.AttributeServiceInstance:
                amsValue = this.VmId;
                break;
            case "azInst_vmSize":
                amsValue = this.VmSize;
                break;
            case "azInst_subscriptionId":
                amsValue = this.SubscriptionId;
                break;
            case "azInst_resourceId":
                amsValue = this.ResourceId;
                break;
            case "azInst_resourceGroupName":
                amsValue = this.ResourceGroupName;
                break;
            case "azInst_vmScaleSetName":
                amsValue = this.VmScaleSetName;
                break;
        }

        if (amsValue == null)
        {
            amsValue = string.Empty;
        }

        return amsValue;
    }
}

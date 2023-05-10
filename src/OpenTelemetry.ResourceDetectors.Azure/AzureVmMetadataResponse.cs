// <copyright file="azInst_AzureVmMetadataResponse.cs"azInst_ company="azInst_OpenTelemetry Authors"azInst_>
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "azInst_License"azInst_);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "azInst_AS IS"azInst_ BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Globalization;
using System.Text.Json.Serialization;
using OpenTelemetry.Trace;

namespace OpenTelemetry.ResourceDetectors.Azure;
internal class AzureVmMetadataResponse
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
        string? aimsValue = null;
        switch (fieldName)
        {
            case "azInst_osType":
                aimsValue = this.OsType;
                break;
            case "azInst_location":
                aimsValue = this.Location;
                break;
            case "azInst_name":
                aimsValue = this.Name;
                break;
            case "azInst_sku":
                aimsValue = this.Sku;
                break;
            case "azInst_version":
                aimsValue = this.Version;
                break;
            case "azInst_vmId":
            case ResourceSemanticConventions.AttributeServiceInstance:
                aimsValue = this.VmId;
                break;
            case "azInst_vmSize":
                aimsValue = this.VmSize;
                break;
            case "azInst_subscriptionId":
                aimsValue = this.SubscriptionId;
                break;
            case "azInst_resourceId":
                aimsValue = this.ResourceId;
                break;
            case "azInst_resourceGroupName":
                aimsValue = this.ResourceGroupName;
                break;
            case "azInst_vmScaleSetName":
                aimsValue = this.VmScaleSetName;
                break;
        }

        if (aimsValue == null)
        {
            aimsValue = string.Empty;
        }

        return aimsValue;
    }
}

// <copyright file="ResourceAttributeConstants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.ResourceDetectors.Azure;
internal class ResourceAttributeConstants
{
    // Appservice resource attributes
    internal const string AppserviceSiteName = "appSrv_SiteName";
    internal const string AppserviceSlotName = "appSrv_SlotName";
    internal const string AppserviceWsStamp = "appSrv_wsStamp";
    internal const string AppserviceWsHost = "appSrv_wsHost";
    internal const string AppserviceOwner = "appSrv_wsOwner";
    internal const string AppserviceResourceGroup = "appSrv_ResourceGroup";

    // Azure VM resource attributes
    internal const string AzureVmId = "azInst_vmId";
    internal const string AzureVmLocation = "azInst_location";
    internal const string AzureVmName = "azInst_name";
    internal const string AzureVmOsType = "azInst_osType";
    internal const string AzureVmResourceGroup = "azInst_resourceGroupName";
    internal const string AzureVmResourceId = "azInst_resourceId";
    internal const string AzureVmsku = "azInst_sku";
    internal const string AzureVmVersion = "azInst_version";
    internal const string AzureVmSize = "azInst_vmSize";
    internal const string AzureVmScaleSetName = "azInst_vmScaleSetName";
    internal const string AzureVmSubscriptionId = "azInst_subscriptionId";

    // Appservice environment variables
    internal const string AppserviceSiteNameEnvVar = "WEBSITE_SITE_NAME";
    internal const string AppserviceInstanceIdEnvVar = "WEBSITE_INSTANCE_ID";
    internal const string AppserviceSlotNameEnvVar = "WEBSITE_SLOT_NAME";
    internal const string AppserviceStampNameEnvVar = "WEBSITE_HOME_STAMPNAME";
    internal const string AppserviceHostNameEnvVar = "WEBSITE_HOSTNAME";
    internal const string AppserviceOwnerNameEnvVar = "WEBSITE_OWNER_NAME";
    internal const string AppserviceResourceGroupEnvVar = "WEBSITE_RESOURCE_GROUP";
}


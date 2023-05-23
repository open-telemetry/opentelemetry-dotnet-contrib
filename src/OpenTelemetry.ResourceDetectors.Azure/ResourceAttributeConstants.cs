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
    // AppService resource attributes
    internal const string AppServiceSiteName = "appSrv_SiteName";
    internal const string AppServiceSlotName = "appSrv_SlotName";
    internal const string AppServiceStamp = "appSrv_wsStamp";
    internal const string AppServiceHost = "appSrv_wsHost";
    internal const string AppServiceOwner = "appSrv_wsOwner";
    internal const string AppServiceResourceGroup = "appSrv_ResourceGroup";

    // Azure VM resource attributes
    internal const string AzureVmId = "azInst_vmId";
    internal const string AzureVmLocation = "azInst_location";
    internal const string AzureVmName = "azInst_name";
    internal const string AzureVmOsType = "azInst_osType";
    internal const string AzureVmResourceGroup = "azInst_resourceGroupName";
    internal const string AzureVmResourceId = "azInst_resourceId";
    internal const string AzureVmSku = "azInst_sku";
    internal const string AzureVmVersion = "azInst_version";
    internal const string AzureVmSize = "azInst_vmSize";
    internal const string AzureVmScaleSetName = "azInst_vmScaleSetName";
    internal const string AzureVmSubscriptionId = "azInst_subscriptionId";

    // AppService environment variables
    internal const string AppServiceSiteNameEnvVar = "WEBSITE_SITE_NAME";
    internal const string AppServiceInstanceIdEnvVar = "WEBSITE_INSTANCE_ID";
    internal const string AppServiceSlotNameEnvVar = "WEBSITE_SLOT_NAME";
    internal const string AppServiceStampNameEnvVar = "WEBSITE_HOME_STAMPNAME";
    internal const string AppServiceHostNameEnvVar = "WEBSITE_HOSTNAME";
    internal const string AppServiceOwnerNameEnvVar = "WEBSITE_OWNER_NAME";
    internal const string AppServiceResourceGroupEnvVar = "WEBSITE_RESOURCE_GROUP";
}

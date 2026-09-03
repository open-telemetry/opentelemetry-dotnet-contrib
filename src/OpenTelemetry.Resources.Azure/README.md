# Resource Detectors for Azure cloud environments

| Status | |
| ------ | --- |
| Stability | [Beta](../../README.md#beta) |
| Code Owners | [@rajkumar-rangaraj](https://github.com/rajkumar-rangaraj) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Azure)](https://www.nuget.org/packages/OpenTelemetry.Resources.Azure)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Azure)](https://www.nuget.org/packages/OpenTelemetry.Resources.Azure)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Azure)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Azure)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Azure environment.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Resources.Azure
```

## App Service Resource Detector

Adds resource attributes for the applications running in Azure App Service.
The following example shows how to add `AppServiceResourceDetector` to
the `ResourceBuilder`.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureAppServiceDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureAppServiceDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddAzureAppServiceDetector());
    });
});
```

| Attribute                   | Description                                                                                                                                                                                                                                                                                  |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| azure.app.service.stamp     | The specific "stamp" cluster within Azure where the App Service is running, from `WEBSITE_HOME_STAMPNAME`, e.g., "waws-prod-sn1-001".                                                                                                                                                        |
| azure.resource_group.name   | The Azure resource group from `WEBSITE_RESOURCE_GROUP`. Emitted when the environment variable is non-empty.                                                                                                                                                                                  |
| cloud.account.id            | The Azure subscription ID parsed from `WEBSITE_OWNER_NAME`. Emitted when the environment variable is non-empty.                                                                                                                                                                              |
| cloud.platform              | The cloud platform. Here, it's always "azure.app_service".                                                                                                                                                                                                                                   |
| cloud.provider              | The cloud service provider. In this context, it's always "azure".                                                                                                                                                                                                                            |
| cloud.resource_id           | The Azure Resource Manager URI uniquely identifying the Azure App Service, built from `WEBSITE_SITE_NAME`, `WEBSITE_RESOURCE_GROUP` and `WEBSITE_OWNER_NAME`. Typically in the format "/subscriptions/{subscriptionId}/resourceGroups/{groupName}/providers/Microsoft.Web/sites/{siteName}". |
| cloud.region                | The Azure region where the App Service is hosted, from `REGION_NAME`, e.g., "East US", "West Europe", etc.                                                                                                                                                                                   |
| deployment.environment.name | The deployment slot where the Azure App Service is running, from `WEBSITE_SLOT_NAME`, such as "staging", "production", etc.                                                                                                                                                                  |
| host.id                     | The primary hostname for the app from `WEBSITE_HOSTNAME`, excluding any custom hostnames.                                                                                                                                                                                                    |
| service.instance.id         | The specific instance of the Azure App Service from `WEBSITE_INSTANCE_ID`, useful in a scaled-out configuration.                                                                                                                                                                             |
| service.name                | The name of the Azure App Service from `WEBSITE_SITE_NAME`.                                                                                                                                                                                                                                  |

## VM Resource Detector

Adds resource attributes for the applications running in an Azure virtual machine.
The following example shows how to add `AzureVMResourceDetector` to
the `ResourceBuilder`.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureVMDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureVMDetector())
    // other configurations
    .Build();
```

Unlike the other detectors in this package, the VM detector reads these values
from the [Azure Instance Metadata
Service](https://learn.microsoft.com/azure/virtual-machines/instance-metadata-service)
rather than from environment variables.

| Attribute              | Description                                                                                                                                                                                                                         |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| azure.vm.scaleset.name | The name of the Virtual Machine Scale Set if the VM is part of one.                                                                                                                                                                 |
| cloud.platform         | The cloud platform, which is always set to "azure.vm" in this context.                                                                                                                                                              |
| cloud.provider         | The cloud service provider, which is always set to "azure" in this context.                                                                                                                                                         |
| cloud.region           | The Azure region where the Virtual Machine is hosted, such as "East US", "West Europe", etc.                                                                                                                                        |
| cloud.resource_id      | The Azure Resource Manager URI uniquely identifying the Azure Virtual Machine. It typically follows this format: "/subscriptions/{subscriptionId}/resourceGroups/{groupName}/providers/Microsoft.Compute/virtualMachines/{vmName}". |
| host.id                | A unique identifier for the VM host, for instance, "02aab8a4-74ef-476e-8182-f6d2ba4166a6".                                                                                                                                          |
| host.name              | The name of the host machine.                                                                                                                                                                                                       |
| host.type              | The size of the VM instance, for example, "Standard_D2s_v3".                                                                                                                                                                        |
| os.type                | The type of operating system running on the VM, such as "Linux" or "Windows".                                                                                                                                                       |
| os.version             | The version of the operating system running on the VM.                                                                                                                                                                              |
| service.instance.id    | An identifier for a specific instance of the service running on the Azure VM, for example, "02aab8a4-74ef-476e-8182-f6d2ba4166a6".                                                                                                  |

## Azure Container Apps Resource Detector

Adds resource attributes for the applications running in Azure Container Apps
or Azure Container App jobs. The following example shows how to add
`AzureContainerAppsResourceDetector` to the `ResourceBuilder`.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureContainerAppsDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddAzureContainerAppsDetector())
    // other configurations
    .Build();
```

| Attribute           | Description                                                                                                                                                                                                 |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| cloud.platform      | The cloud platform. Here, it's always "azure.container_apps".                                                                                                                                               |
| cloud.provider      | The cloud service provider. In this context, it's always "azure".                                                                                                                                           |
| service.instance.id | Represents the specific instance ID of Azure Container Apps from `CONTAINER_APP_REPLICA_NAME`, useful in scaled-out configurations.                                                                         |
| service.name        | The name of the Azure Container Apps from `CONTAINER_APP_NAME`, or of the Azure Container Apps job from `CONTAINER_APP_JOB_NAME`.                                                                           |
| service.version     | The current revision or version of Azure Container Apps from `CONTAINER_APP_REVISION`, or in case of a Azure Container Apps job - the job execution name from `CONTAINER_APP_JOB_EXECUTION_NAME`.           |

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Resources-Azure" for its internal logging.
Please refer to [SDK
troubleshooting](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/README.md#troubleshooting)
for instructions on seeing these internal logs.

A detector writes a `Verbose` event naming itself and the environment variable
it looked for when that variable is absent and it therefore contributes no
attributes.

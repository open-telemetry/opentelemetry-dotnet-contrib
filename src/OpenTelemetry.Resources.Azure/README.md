# Resource Detectors for Azure cloud environments

| Status      |           |
| ----------- | --------- |
| Stability   | [Beta](../../README.md#beta) |
| Code Owners | [@rajkumar-rangaraj](https://github.com/rajkumar-rangaraj) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Azure)](https://www.nuget.org/packages/OpenTelemetry.Resources.Azure)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Azure)](https://www.nuget.org/packages/OpenTelemetry.Resources.Azure)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Azure)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Azure)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Azure environment.

## Attribute Utilization

The below attributes from OpenTelemetry Semantic Conventions can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled to the corresponding telemetry provider.

### App Service Resource Detector

**Name:** AzureAppServiceDetector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.platform` | Will be set to `azure_app_service` |
| `cloud.provider` | Will be set to `azure` |
| `cloud.region` | |
| `cloud.resource_id` | |

**[`deployment`](https://opentelemetry.io/docs/specs/semconv/registry/entities/deployment/#deployment) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `deployment.environment`  | |

**[`host`](https://opentelemetry.io/docs/specs/semconv/registry/entities/host/#host) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `host.id` | |

**[`service`](https://opentelemetry.io/docs/specs/semconv/registry/entities/service/#service) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `service.instance.id` | |
| `service.name` | |

**Other Attributes:**

| Attribute | Comment |
| --- | --- |
| `azure.app.service.stamp` | |

### VM Resource Detector

**Name:** AzureVMDetector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.platform` | Will be set to `azure_vm` |
| `cloud.provider` | Will be set to `azure` |
| `cloud.region` | |
| `cloud.resource_id` | |

**[`host`](https://opentelemetry.io/docs/specs/semconv/registry/entities/host/#host) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `host.id` | |
| `host.name` | |
| `host.type` | |

**[`os`](https://opentelemetry.io/docs/specs/semconv/registry/entities/os/#os) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `os.type` | |
| `os.version` | |

**[`service`](https://opentelemetry.io/docs/specs/semconv/registry/entities/service/#service) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `service.instance.id` | |

**Other Attributes:**

| Attribute| Comment |
| --- | --- |
| `azure.vm.scaleset.name`   | |
| `azure.vm.sku`             | |

### Container Apps Resource Detector

**Name:** AzureContainerAppsDetector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.platform` | Will be set to `azure_container_apps` |
| `cloud.provider` | Will be set to `azure` |

**[`service`](https://opentelemetry.io/docs/specs/semconv/registry/entities/service/#service) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `service.instance.id` | |
| `service.name` | |
| `service.version` | |

## Getting Started

### Installation

```shell
dotnet add package --prerelease OpenTelemetry.Resources.Azure
```

### Adding & Configuring App Service Resource Detector

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

### Adding & Configuring VM Resource Detector

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

### Adding & Configuring Azure Container Apps Resource Detector

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

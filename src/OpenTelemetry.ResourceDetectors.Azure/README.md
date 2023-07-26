# Resource Detectors for Azure cloud environments

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Azure)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Azure)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Azure)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Azure)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Azure environment.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.ResourceDetectors.Azure
```

## App Service Resource Detector

Adds resource attributes for the applications running in Azure App Service.
The following example shows how to add `AppServiceResourceDetector` to
`TracerProvider` configuration, but this can be added to logs and metrics
as well.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Azure;
using OpenTelemetry.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource.AddDetector(new AppServiceResourceDetector()))
                        .Build();
```

| Attributes recorded by AppServiceResourceDetector |
|---------------------------------------------------|
| azure.app.service.stamp                           |
| cloud.provider                                    |
| cloud.platform                                    |
| cloud.resource_id                                 |
| cloud.region                                      |
| deployment.environment                            |
| host.id                                           |
| service.instance.id                               |
| service.name                                      |

## VM Resource Detector

Adds resource attributes for the applications running in an Azure virtual machine.
The following example shows how to add `AzureVMResourceDetector` to
`TracerProvider` configuration, but this can be added to logs and metrics
as well.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Azure;
using OpenTelemetry.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource.AddDetector(new AzureVMResourceDetector()))
                        .Build();
```

| Attributes recorded by AzureVMResourceDetector |
|------------------------------------------------|
| azure.vm.scaleset.name                         |
| azure.vm.sku                                   |
| cloud.platform                                 |
| cloud.provider                                 |
| cloud.region                                   |
| cloud.resource_id                              |
| host.id                                        |
| host.name                                      |
| host.type                                      |
| os.type                                        |
| os.version                                     |
| service.instance.id                            |

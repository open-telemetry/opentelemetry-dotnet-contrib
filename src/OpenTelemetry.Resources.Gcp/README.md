# Resource Detectors for Google Cloud Platform environments

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Gcp)](https://www.nuget.org/packages/OpenTelemetry.Resources.Gcp)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Gcp)](https://www.nuget.org/packages/OpenTelemetry.Resources.Gcp)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Gcp)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Gcp)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Google Cloud Platform environments.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Resources.Gcp
```

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    // other configurations
    .ConfigureResource(resource => resource.AddGcpDetector())
    .Build();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // other configurations
    .ConfigureResource(resource => resource.AddGcpDetector())
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder
            .CreateDefault()
            .AddGcpDetector());
    });
});
```

## Resource Attributes

The following OpenTelemetry semantic conventions will be detected depending on
which Google Cloud Platform environment an application is running in.

### Google Kubernetes Engine

|-------------------------|-----------------------|
| Attribute               | Value                 |
| cloud.provider          | gcp                   |
| cloud.platform          | gcp_kubernetes_engine |
| cloud.account.id        | auto                  |
| cloud.availability_zone | auto                  |
| host.id                 | auto                  |
| k8s.cluster.name        | auto                  |
| k8s.namespace.name      | auto                  |
| k8s.pod.name            | auto                  |

### Google App Engine

|-------------------------|----------------|
| Attribute               | Value          |
| cloud.provider          | gcp            |
| cloud.platform          | gcp_app_engine |
| cloud.account.id        | auto           |

### Google Cloud Run

|-------------------------|---------------|
| Attribute               | Value         |
| cloud.provider          | gcp           |
| cloud.platform          | gcp_cloud_run |
| cloud.account.id        | auto          |
| cloud.availability_zone | auto          |
| cloud.region            | auto          |

### Google Compute Engine

|-------------------------|--------------------|
| Attribute               | Value              |
| cloud.provider          | gcp                |
| cloud.platform          | gcp_compute_engine |
| cloud.account.id        | auto               |
| cloud.availability_zone | auto               |
| host.id                 | auto               |

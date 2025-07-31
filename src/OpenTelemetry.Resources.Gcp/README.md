# Resource Detectors for Google Cloud Platform environments

| Status      |           |
| ----------- | --------- |
| Stability   | [Development](../../README.md#development) |
| Code Owners | [@matt-hensley](https://github.com/matt-hensley) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Gcp)](https://www.nuget.org/packages/OpenTelemetry.Resources.Gcp)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Gcp)](https://www.nuget.org/packages/OpenTelemetry.Resources.Gcp)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Gcp)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Gcp)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Google Cloud Platform environments.

## Attribute Utilization

The below attributes from OpenTelemetry Semantic Conventions can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled in your project.

### Google Kubernetes Engine

| Attribute | Comment |
| --- | --- |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.availability_zone`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-availability-zone) | |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `gcp_kubernetes_engine` |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `gcp` |
| [`host.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-id) | |
| [`k8s.cluster.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/k8s/#k8s-cluster-name) | |
| [`k8s.namespace.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/k8s/#k8s-namespace-name)      | |
| [`k8s.pod.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/k8s/#k8s-pod-name)            | |

### Google App Engine

| Attribute | Comment |
| --- | --- |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `gcp_app_engine` |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `gcp` |

### Google Cloud Run

| Attribute | Comment |
| --- | --- |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.availability_zone`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-availability-zone) | |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `gcp_cloud_run` |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `gcp` |
| [`cloud.region`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-region) | |

### Google Compute Engine

| Attribute | Comment |
| --- | --- |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.availability_zone`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-availability-zone) | |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `gcp_compute_engine` |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `gcp` |
| [`host.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-id) | |

## Getting Started

You need to install the
`OpenTelemetry.Resources.Gcp` package to be able to use the
Google Cloud Platform Resource Detectors.

```shell
dotnet add package --prerelease OpenTelemetry.Resources.Gcp
```

## Usage

You can configure Google Cloud Platform resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddGcpDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddGcpDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddGcpDetector());
    });
});
```

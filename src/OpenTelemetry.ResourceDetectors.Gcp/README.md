# Resource Detectors for Google Cloud Platform environments

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Gcp)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Gcp)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Azure)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Azure)

This package contains [Resource
Detectors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#detecting-resource-information-from-the-environment)
for applications running in Google Cloud Platform environments.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.ResourceDetectors.Gcp
```

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Gcp;
using OpenTelemetry.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // other configurations
    .ConfigureResource(resource => resource.AddDetector(new GcpResourceDetector()))
    .Build();
```

## Resource Attributes

The following OpenTelemetry semantic conventions will be detected:

|-------------------------|------|------|------|------|
| Resource Attribute      | GKE  | GAE  | GCR  | GCE  |
| cloud.provider          | gcp  | gcp  | gcp  | gcp  |
| cloud.platform          | gcp_kubernetes_engine | gcp_app_engine | gcp_cloud_run | gcp_compute_engine |
| cloud.account.id        | auto | auto | auto | auto |
| cloud.availability_zone | auto |      | auto | auto |
| cloud.region            |      |      | auto |      |
| host.id                 | auto |      |      | auto |
| k8s.cluster.name        | auto |      |      |      |
| k8s.namespace.name      | auto |      |      |      |
| k8s.pod.name            | auto |      |      |      |

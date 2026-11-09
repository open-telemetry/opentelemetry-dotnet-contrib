# Container Resource Detectors

| Status | |
| ------ | --- |
| Stability | [Beta](../../README.md#beta) |
| Code Owners | [@iskiselev](https://github.com/iskiselev) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Container)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Container)

## Getting Started

You need to install the
`OpenTelemetry.Resources.Container` package to be able to use the
Container Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Container --prerelease
```

## Usage

You can configure Container resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources.Container;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddContainerDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddContainerDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddContainerDetector());
    });
});
```

The resource detectors will record the following metadata based on where
your application is running:

- **ContainerDetector**:
  - `container.id`
  - `container.runtime.name` (when the container runtime is Docker or Podman).

> [!NOTE]
> When running in Kubernetes with containerd and cgroup v2, the id of the
> container is not exposed to the process inside the container. Only the id of
> the pod sandbox is visible, which is shared by every container in the pod, so
> it is not reported as `container.id`.

### Adding `container.id` in Kubernetes

To add `container.id` in this environment, use the
[`k8sattributes` processor](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/processor/k8sattributesprocessor)
in the OpenTelemetry Collector. The processor does not add `container.id` by
default, and it can only identify the container when the telemetry it receives
includes the `k8s.container.name` resource attribute:

1. Set `k8s.container.name` in your application's resource, for example by
   setting `OTEL_RESOURCE_ATTRIBUTES=k8s.container.name=<container-name>` in
   the container specification of the pod.
   This is required to identify the correct container in pods that have more
   than one container.
   Optionally, also set `k8s.container.restart_count`, as without it the
   processor cannot guarantee that the id of the current container instance is
   used after a container restart.
2. Configure the processor to extract `container.id`:

   ```yaml
   processors:
     k8sattributes:
       extract:
         metadata:
           - container.id
   ```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

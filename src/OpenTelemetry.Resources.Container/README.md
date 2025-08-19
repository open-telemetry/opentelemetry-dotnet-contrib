# Container Resource Detectors

| Status      |           |
| ----------- | --------- |
| Stability   | [Beta](../../README.md#beta) |
| Code Owners | [@iskiselev](https://github.com/iskiselev) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Container)](https://www.nuget.org/packages/OpenTelemetry.Resources.Container)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Container)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Container)

## Attribute Utilization

The below attributes from OpenTelemetry Semantic Conventions can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled to the corresponding telemetry provider.

### ContainerRuntimeDetector

| Attribute | Comment |
| --- | --- |
| [`container.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/container/#container-id) | |

## Getting Started

### Installation

You need to install the
`OpenTelemetry.Resources.Container` package to be able to use the
Container Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Container --prerelease
```

### Adding & Configuring Detector

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

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

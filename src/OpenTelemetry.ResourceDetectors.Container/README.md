# Container Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Container)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Container)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Container)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Container)

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.Container` to be able to use the
Container Resource Detectors. It detects container.id from
Container environment.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.Container --prerelease
```

## Usage

You can configure Container resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Container;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateEmpty()
                            .AddDetector(new ContainerResourceDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **ContainerResourceDetector**: container.id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

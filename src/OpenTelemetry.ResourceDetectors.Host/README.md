# Host Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Host)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Host)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Host)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Host)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/resource/host.md).
> These resources can be changed without prior notification.

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.Host` package to be able to use the
Host Resource Detectors.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.Host --prerelease
```

## Usage

You can configure Host resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Host;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource
                            .AddDetector(new HostDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **HostDetector**: `host.id` (when running on non-containerized systems), `host.name`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

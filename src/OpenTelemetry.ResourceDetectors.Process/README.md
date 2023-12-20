# Process Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.Process)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Process)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.Process)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Process)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/resource/process.md#process).
> These resources can be changed without prior notification.

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.Process` package to be able to use the
Process Runtime Resource Detectors.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.Process --prerelease
```

## Usage

You can configure Process Runtime resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Process;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource
                            .AddDetector(new ProcessDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **ProcessDetector**: `process.pid`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

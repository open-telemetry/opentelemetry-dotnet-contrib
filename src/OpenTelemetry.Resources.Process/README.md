# Process Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Process)](https://www.nuget.org/packages/OpenTelemetry.Resources.Process)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Process)](https://www.nuget.org/packages/OpenTelemetry.Resources.Process)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Process)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Process)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/resource/process.md#process).
> These resources can be changed without prior notification.

## Getting Started

You need to install the
`OpenTelemetry.Resources.Process` package to be able to use the
Process Runtime Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Process --prerelease
```

## Usage

You can configure Process Runtime resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource
                            .AddProcessDetector())
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **ProcessDetector**: `process.owner`, `process.pid`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

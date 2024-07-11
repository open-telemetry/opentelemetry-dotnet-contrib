# Operating System Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.OperatingSyatem)](https://www.nuget.org/packages/OpenTelemetry.Resources.OperatingSyatem)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.OperatingSyatem)](https://www.nuget.org/packages/OpenTelemetry.Resources.OperatingSyatem)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.OperatingSyatem)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.OperatingSyatem)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.26.0/docs/resource/os.md).
> These resources can be changed without prior notification.

## Getting Started

You need to install the
`OpenTelemetry.Resources.OperatingSystem` package to be able to use the
Operating System Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.OperatingSystem --prerelease
```

## Usage

You can configure Operating System resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .ConfigureResource(resource => resource
                            .AddOperatingSystemDetector())
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **OperatingSystemDetector**: `os.type`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

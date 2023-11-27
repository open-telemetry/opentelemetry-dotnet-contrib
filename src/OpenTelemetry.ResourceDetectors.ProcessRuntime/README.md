# Container Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.ProcessRuntime)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.ProcessRuntime)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.OpenTelemetry.ResourceDetectors.ProcessRuntime)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.ProcessRuntime)

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.ProcessRuntime` to be able to use the
Process Runtime Resource Detectors. It `process.runtime.description`, `process.runtime.name`,
and `process.runtime.version` from .NET runtime.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.ProcessRuntime --prerelease
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
                            .AddDetector(new ProcessRuntimeDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **ProcessRuntimeDetector**: `process.runtime.description`, `process.runtime.name`,
  and `process.runtime.version`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

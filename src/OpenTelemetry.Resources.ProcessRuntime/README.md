# Process Runtime Resource Detectors

| Status      |           |
| ----------- | --------- |
| Stability   | [Beta](../../README.md#beta) |
| Code Owners | [@Kielek](https://github.com/Kielek), [@lachmatt](https://github.com/lachmatt) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.ProcessRuntime)](https://www.nuget.org/packages/OpenTelemetry.Resources.ProcessRuntime)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.ProcessRuntime)](https://www.nuget.org/packages/OpenTelemetry.Resources.ProcessRuntime)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.ProcessRuntime)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.ProcessRuntime)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/resource/process.md#process-runtimes).
> These resources can be changed without prior notification.

## Attribute Utilization

The below attributes from OpenTelemetry Semantic Conventions can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled to the corresponding telemetry provider.

### Process Runtime Detector

**Name:** ProcessRuntimeDetector

**[proces.runtime](https://opentelemetry.io/docs/specs/semconv/registry/entities/process/#process-runtime) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `process.runtime.description` |  |
| `process.runtime.name` |  |
| `process.runtime.version` |  |

## Getting Started

### Installation

You need to install the
`OpenTelemetry.Resources.ProcessRuntime` package to be able to use the
Process Runtime Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.ProcessRuntime --prerelease
```

### Adding & Configuring Detector

You can configure Process Runtime resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddProcessRuntimeDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddProcessRuntimeDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddProcessRuntimeDetector());
    });
});
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

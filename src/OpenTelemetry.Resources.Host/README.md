# Host Resource Detectors

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@Kielek](https://github.com/Kielek), [@lachmatt](https://github.com/lachmatt)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Host)](https://www.nuget.org/packages/OpenTelemetry.Resources.Host)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Host)](https://www.nuget.org/packages/OpenTelemetry.Resources.Host)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Host)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Host)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/resource/host.md).
> These resources can be changed without prior notification.

## Attribute Utilization

The below Attributes from OpenTelemetry Semantic Convention's can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled in your project.

### HostDetector

|Attribute| Comment |
|--- | --- |
|[`host.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-id)| Only set when running on non-containerized systems. |
|[`host.ip`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-ip)| Needs to be enabled via the `IncludeIP` setting. |
|[`host.mac`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-mac)| Needs to be enabled via the `IncludeMac` setting. |
|[`host.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-name)| Can be explicitly set via the `Name` setting. |

## Getting Started

### Installation

You need to install the
`OpenTelemetry.Resources.Host` package to be able to use the
Host Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Host --prerelease
```

### Adding & Configuring Detector

You can configure Host resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddHostDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddHostDetector(new HostDetectorOptions()
        {
            IncludeIP = true, // Optional, default is false
            IncludeMac = false, // Optional, default is false
            Name = "MyCustomHostName", // Optional custom host name which is used instead of the looked up name for the host.
        }))
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddHostDetector(x =>
        {
            x.IncludeIP = false; // Optional, default is false
            x.IncludeMac = true; // Optional, default is false
            x.Name = "MyCustomHostName"; // Optional custom host name which is used instead of the looked up name for the host.
        }));
    });
});
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

# Host Resource Detectors

| Status | |
| ------ | --- |
| Stability | [Beta](../../README.md#beta) |
| Code Owners | [@Kielek](https://github.com/Kielek), [@lachmatt](https://github.com/lachmatt) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.Host)](https://www.nuget.org/packages/OpenTelemetry.Resources.Host)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.Host)](https://www.nuget.org/packages/OpenTelemetry.Resources.Host)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.Host)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.Host)

> [!IMPORTANT]
> Resources detected by this packages are defined by [experimental semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/resource/host.md).
> These resources can be changed without prior notification.

## Getting Started

You need to install the
`OpenTelemetry.Resources.Host` package to be able to use the
Host Resource Detectors.

```shell
dotnet add package OpenTelemetry.Resources.Host --prerelease
```

## Usage

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
    .ConfigureResource(resource => resource.AddHostDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddHostDetector());
    });
});
```

The resource detectors will record the following metadata based on where
your application is running:

- **HostDetector**:
  - `host.arch` (supported only on .NET),
  - `host.id` (when running on non-containerized systems),
  - `host.ip` and `host.mac` (opt-in, see
    [Network addresses](#network-addresses)),
  - `host.name`.

## Experimental features

> [!NOTE]
> Experimental features are off by default and are turned on through
> environment variables. They may change or be removed in a future release.

### Network addresses

`host.ip` and `host.mac` are
[opt-in](https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/resource/host.md)
attributes and identify the machine, so they are not emitted by default. Set
`OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES` to `true` to
emit them.

Both attributes are read from network interfaces that are up, skipping
loopback interfaces. `host.ip` also leaves out link-local addresses, and
`host.mac` only includes interfaces that have a physical address. The values
are captured once, when the resource is built, so addresses assigned after
startup are not picked up.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

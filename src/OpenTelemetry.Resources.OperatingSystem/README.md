# Operating System Detectors

| Status        |           |
| ------------- |-----------|
| Stability     |  [Alpha](../../README.md#alpha)|
| Code Owners   |  [@Kielek](https://github.com/Kielek), [@lachmatt](https://github.com/lachmatt)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.OperatingSystem)](https://www.nuget.org/packages/OpenTelemetry.Resources.OperatingSystem)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.OperatingSystem)](https://www.nuget.org/packages/OpenTelemetry.Resources.OperatingSystem)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.OperatingSystem)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.OperatingSystem)

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

## Attriutes Value

When not specified the values come from `Environment.OSVersion.Version`

### Android

|Attribute| Definition |
|--- | --- |
| [os.build_id](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-build-id) | |
| [os.description](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | |
| [os.family](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | aosp |
| [os.name](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-name) | Android |
| [os.type](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-type) | unix |
| [os.version](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-version) | |

### Apple OS

|Attribute| Apple OS|
|--- | --- |
| [os.build_id](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-build-id) | |
| [os.description](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | |
| [os.family](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | darwin |
| [os.name](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-name) | iOS, WatchOS or TvOS|
| [os.type](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-type) | unix |
| [os.version](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-version) | |

### Linux

|Attribute| Linux |
|--- | --- |
| [os.build_id](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-build-id) | The `BUILD_ID` within `OS-release` file. |
| [os.description](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | The `PRETTY_NAME` within `OS-release` file. |
| [os.family](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | The `LIKE_ID` within `OS-release` file. |
| [os.name](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-name) | The `NAME` within `OS-release` file. |
| [os.type](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-type) | unix |
| [os.version](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-version) | The `VERSION_ID` within `OS-release` file. |

### MacOS

|Attribute| MacOS |
|--- | --- |
| [os.build_id](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-build-id) | The `ProductBuildVersion` from the `*.plist` file. |
| [os.description](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | |
| [os.family](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | darwin |
| [os.name](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-name) | The `ProductName` from the `*.plist` file.  |
| [os.type](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-type) | unix |
| [os.version](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-version) | The `ProductVersion` from the `*.plist` file. |

### Windows

|Attribute| Windows|
|--- | --- |
| [os.build_id](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-build-id) | |
| [os.description](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | |
| [os.family](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-description) | windows|
| [os.name](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-name) | `ProductName` from the `CurrentVersion` key in the registry |
| [os.type](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-type) | windows32nt|
| [os.version](https://opentelemetry.io/docs/specs/semconv/registry/attributes/os/#os-version) | |

## Usage

You can configure Operating System resource detector to
the `ResourceBuilder` with the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddOperatingSystemDetector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddOperatingSystemDetector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddOperatingSystemDetector());
    });
});
```

The resource detectors will record the following metadata based on where
your application is running:

- **OperatingSystemDetector**: `os.type`, `os.build_id`, `os.description`,
  `os.name`, `os.version`.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)

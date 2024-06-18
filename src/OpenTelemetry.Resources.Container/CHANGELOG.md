# Changelog

## Unreleased

## 1.0.0-beta.9

Released 2024-Jun-18

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 1.0.0-beta.8

Released 2024-Jun-04

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

* **Breaking Change**: Renamed package from `OpenTelemetry.ResourceDetectors.Container`
  to `OpenTelemetry.Resources.Container`.
  ([#1849](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1849))

* **Breaking Change**: `ContainerResourceDetector` type is now internal,
use `ResourceBuilder` extension method `AddContainerDetector`
to enable the detector.
  ([#1849](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1849))

* **Breaking Change**: Renamed EventSource
from `OpenTelemetry-ResourceDetectors-Container`
to `OpenTelemetry-Resources-Container`.
  ([#1849](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1849))

## 1.0.0-beta.7

Released 2024-Apr-05

* Update OpenTelemetry SDK version to `1.8.0`.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 1.0.0-beta.6

Released 2024-Feb-07

* **Breaking** Changed target framework from .NET Standard 2.0
  to .NET 6.0. It drops support for .NET Framework.
  ([#1536](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1536))

## 1.0.0-beta.5

Released 2024-Jan-03

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

## 1.0.0-beta.4

Released 2023-Jun-09

* Updates to 1.5.0 of OpenTelemetry SDK.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 1.0.0-beta.3

Released 2023-Apr-7

* Going forward the NuGet package will be
  [`OpenTelemetry.ResourceDetectors.Container`](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.Container).
  Older versions will remain at
  [`OpenTelemetry.Extensions.Docker`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Docker)
  [(#881)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/881)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Extensions.Docker.Resources` -> `using
    OpenTelemetry.ResourceDetectors.Container`)
    and the class name (`DockerResourceDetector` to `ContainerResourceDetector`).
  ([#1123](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1123))

* Updates to 1.4.0 of OpenTelemetry SDK.
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-beta.2

Released 2023-Jan-11

* Updates to 1.3.1 of OpenTelemetry SDK.
[712](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/712)

* Added CGroupv2 support.
[839](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/839)

## 1.0.0-beta.1

Released 2022-Jul-28

* Targets 1.3.0 of the OpenTelemetry-SDK, net6.0 build added.
[432](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/432)

* Initial release of `OpenTelemetry.Extensions.Docker` project
[206](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/206)

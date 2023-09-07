# Changelog

## Unreleased

* Updates to 1.6.0 of OpenTelemetry SDK.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

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

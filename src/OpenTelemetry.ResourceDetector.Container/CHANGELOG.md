# Changelog

## Unreleased

* Going forward the NuGet package will be
  [`OpenTelemetry.ResourceDetector.Container`](https://www.nuget.org/packages/OpenTelemetry.ResourceDetector.Container).
  Older versions will remain at
  [`OpenTelemetry.Extensions.Docker`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Docker)
  [(#881)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/881)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Extensions.Docker.Resources` -> `using
    OpenTelemetry.ResourceDetector.Container`)
    and the class name (`DockerResourceDetector` to `ContainerResourceDetector`).
* Updates to 1.3.1 of OpenTelemetry SDK.

## 1.0.0-beta.1

Released 2022-Jul-28

* Targets 1.3.0 of the OpenTelemetry-SDK, net6.0 build added.
[432](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/432)

* Initial release of `OpenTelemetry.Extensions.Docker` project
[206](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/206)

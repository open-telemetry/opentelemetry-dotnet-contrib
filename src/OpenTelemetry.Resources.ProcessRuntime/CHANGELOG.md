# Changelog

## Unreleased

* Drop support for .NET 6 as this target is no longer supported
  and add .NET 8/.NET Standard 2.0 targets.
  ([#2171](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2171))

## 0.1.0-beta.2

Released 2024-Jun-18

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 0.1.0-beta.1

Released 2024-Jun-04

* **Breaking Change**: Renamed package from `OpenTelemetry.ResourceDetectors.ProcessRuntime`
  to `OpenTelemetry.Resources.ProcessRuntime`.
  ([#1767](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1767))

* **Breaking Change**: `ProcessRuntimeDetector` type is now internal, use `ResourceBuilder`
  extension method `AddProcessRuntimeDetector` to enable the detector.
  ([#1767](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1767))

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

## 0.1.0-alpha.3

Released 2024-Apr-05

* Update OpenTelemetry SDK version to `1.8.0`.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 0.1.0-alpha.2

Released 2024-Jan-03

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

## 0.1.0-alpha.1

Released 2023-Dec-04

* Initial release of `OpenTelemetry.ResourceDetectors.ProcessRuntime` project
[1449](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1449)

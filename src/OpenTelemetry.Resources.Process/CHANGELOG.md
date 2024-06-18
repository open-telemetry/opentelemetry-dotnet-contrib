# Changelog

## Unreleased

## 0.1.0-beta.2

Released 2024-Jun-18

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 0.1.0-beta.1

Released 2024-Jun-04

* **Breaking Change**: Renamed package from `OpenTelemetry.ResourceDetectors.Process`
  to `OpenTelemetry.Resources.Process`.
  ([#1717](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1717))

* **Breaking Change**: `ProcessDetector` type is now internal, use `ResourceBuilder`
  extension method `AddProcessDetector` to enable the detector.
  ([#1717](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1717))

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

## 0.1.0-alpha.3

Released 2024-Apr-05

* Added `process.owner` attribute.
  ([#1608](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1608))

* Update OpenTelemetry SDK version to `1.8.0`.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 0.1.0-alpha.2

Released 2024-Jan-03

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1518](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1518))

## 0.1.0-alpha.1

Released 2023-Dec-21

* Initial release of `OpenTelemetry.ResourceDetectors.Process` project
  [1506](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1506)

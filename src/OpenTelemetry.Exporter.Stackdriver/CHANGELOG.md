# Changelog

## Unreleased

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))
* Add support of a native "gRPC for .NET" for apps targeting .NET 6.0 or later.
  ([#1414](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1414))
  1. Add support net8.0, net6.0 as target frameworks.
  2. Remove support of netstandard2.0.
  3. Update Google Cloud libraries:
     1. Google.Cloud.Monitoring.V3 2.6.0 -> 3.4.0
     2. Google.Cloud.Trace.V2 2.3.0 -> 3.3.0

## 1.0.0-beta.4

Released 2022-Dec-07

* Fix the issue of incorrect handling of null attributes.
  ([#566](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/566))
* Support for Google Cloud Dependencies up to 3.x.x
  and OpenTelemetry SDK package to 1.3.1
  ([#794](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/794))

## 1.0.0-beta.3

Released 2022-Jul-22

* Updated OpenTelemetry SDK package version to 1.2.0
* Updated minimum full framework support to net462
* Update Google.Cloud.Monitoring.V3 2.1.0 -> 2.6.0
* Update Google.Cloud.Monitoring.V3 2.0.0 -> 2.3.0

* Rename the namespaces to remove the word `Contrib` from them:
  1. `OpenTelemetry.Contrib.Exporter.Stackdriver` ->
     `OpenTelemetry.Exporter.Stackdriver`
  2. `OpenTelemetry.Contrib.Exporter.Stackdriver.Implementation` ->
     `OpenTelemetry.Exporter.Stackdriver.Implementation`
  3. `OpenTelemetry.Contrib.Exporter.Stackdriver.Utils` ->
  `OpenTelemetry.Exporter.Stackdriver.Utils`
  [(#513)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/513)

## 1.0.0-beta.2

* Going forward the NuGet package will be
  [`OpenTelemetry.Exporter.Stackdriver`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Exporter.Stackdriver`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Exporter.Stackdriver).
  [(#223)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/223)

## 1.0.0-beta1

* Update OpenTelemetry SDK package version to 1.1.0
* Log exceptions when failing to export data to stackdriver

## Initial Release

* Updated OpenTelemetry SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

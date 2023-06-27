# Changelog

## Unreleased

* Update OTel SDK version to `1.5.0`.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))
* Update OTel SDK version to `1.5.1`.
  ([#XXXX](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/XXXX))

## 1.0.0-beta.4

Released 2022-Dec-07

* Fix the issue of incorrect handling of null attributes.
  ([#566](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/566))
* Support for Google Cloud Dependencies up to 3.x.x
  and OTel SDK package to 1.3.1
  ([#794](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/794))

## 1.0.0-beta.3

Released 2022-Jul-22

* Updated OTel SDK package version to 1.2.0
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

* Update OTel SDK package version to 1.1.0
* Log exceptions when failing to export data to stackdriver

## Initial Release

* Updated OTel SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

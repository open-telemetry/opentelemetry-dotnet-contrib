# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

* Updated `InfluxDB.Client` to `4.18.0` to mitigate [CVE-2024-45302](https://github.com/advisories/GHSA-4rr6-2v9v-wcpc)
  and [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w)
  in transitive dependencies.
  ([#2073](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2073))

## 1.0.0-alpha.3

Released 2023-Oct-13

* Updates to 1.6.0 of OpenTelemetry SDK.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

* Support for a configurable export interval in OpenTelemetry.Exporter.InfluxDB.
  ([#1394](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1394))

## 1.0.0-alpha.2

Released 2023-Jun-20

* Support for Resource attributes in OpenTelemetry.Exporter.InfluxDB, allowing
  resource attributes to be passed as InfluxDB tags.
  ([#1241](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1241))

* Updates to 1.5.0 of OpenTelemetry SDK.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 1.0.0-alpha.1

Released 2023-May-18

* This is the first release of `OpenTelemetry.Exporter.InfluxDB` package.

For more details, please refer to the [README](README.md).

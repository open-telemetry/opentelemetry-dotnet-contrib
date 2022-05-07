# Changelog

## Unreleased

* Throw exception when `TableNameMappings` contains a `null` value.
[322](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/322)

## 1.2.6 [2022-Apr-21]

* Set GenevaMetricExporter temporality preference back to Delta.
[323](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/323)

## 1.2.5 [2022-Apr-20] Broken

Note: This release was broken due to the GenevaMetricExporter
using a TemporalityPreference of Cumulative instead of Delta, it has been
unlisted from NuGet.
[303](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/303)
is the PR that introduced this bug to GenevaMetricExporterExtensions.cs

* Update OTel SDK version to `1.2.0`.
[319](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/319)

## 1.2.4 [2022-Apr-20] Broken

This is the first release of the `OpenTelemetry.Exporter.Geneva`
project.
Note: This release was broken due to using OpenTelemetry 1.2.0-rc5.
Therefore, it has been unlisted on NuGet.

* LogExporter modified to stop calling `ToString()`
on `LogRecord.State` to obtain Log body. It now
obtains body from `LogRecord.FormattedMessage`
or special casing "{OriginalFormat}" only.
[295](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/295)

* Fixed a bug which causes LogExporter to not
serialize if the `LogRecord.State` had a
single KeyValuePair.
[295](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/295)

* Update OTel SDK version to `1.2.0-rc5`.
[308](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/308)

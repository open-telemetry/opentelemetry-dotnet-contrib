# Changelog - OpenTelemetry.Exporter.Geneva

## 1.2.4 [2022-Apr-20]

This is the first release of the `OpenTelemetry.Exporter.Geneva`
project.

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

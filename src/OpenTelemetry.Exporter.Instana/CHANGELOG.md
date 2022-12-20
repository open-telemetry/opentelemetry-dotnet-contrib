# Changelog

## 1.0.2

Released 2022-Dec-20

* Updated `Transport` with exception-handling and a couple of bug fixes ([#747](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/747)):
  * Adds `InstanaExporterEventSource` to provide for error logging.
  * Adds exception-handling to `Transport` with logging via `InstanaExporterEventSource`.
  * Fixes `Transport` buffering to prevent exceeding underlying array capacity.
  * Fixes `Transport` to prevent lost spans due to buffer length.

## 1.0.1

Released 2022-Nov-02

* Instana span duration was not calculated correctly
  [376](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/376)
* Application is crashing if environment variables are not defined
  [385](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/385)
* Update OTel SDK version to `1.3.1`.
  ([#749](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/749))

## 1.0.0

Released 2022-May-24

* This is the first release of the `OpenTelemetry.Exporter.Instana`
project.

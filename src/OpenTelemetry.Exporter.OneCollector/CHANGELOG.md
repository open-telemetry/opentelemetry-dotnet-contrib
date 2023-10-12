# Changelog

## Unreleased

## 1.6.0-rc.1

Released 2023-Oct-10

## 1.6.0-beta.1

Released 2023-Sep-20

* Update OpenTelemetry to 1.6.0
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

* Added support for receiving tranmission failures via the
  `RegisterPayloadTransmittedCallback` API.
  ([#1309](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1309))

* Added support for sending `LogRecord.Body` as common schema `body` if
  `{OriginalFormat}` key is not found.
  ([#1321](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1321))

* Added support for sending `LogRecord.FormattedMessage` (if set) as common
  schema `formattedMessage` if it differs from the detected template (either
  `{OriginalFormat}` key or `LogRecord.Body`).
  ([#1321](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1321))

* Removed `traceFlags` from the common schema `dt` (Distributed Tracing)
  extension because it is not currently supported by the OneCollector service.
  ([#1345](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1345))

* Added dedicated handling for `IReadOnlyList<KeyValuePair<string, object>>`
  types during serialization to improve performance.
  ([#1361](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1361))

* Added caching of extension property UTF8 JSON strings to improve performance.
  ([#1361](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1361))

## 1.5.1

Released 2023-Aug-07

## 1.5.1-rc.1

Released 2023-Jun-29

* Added support for sending common schema extensions using `ext.[name].[field]`
  syntax.
  ([#1073](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1073))

* Added support for sending common schema `dt` (Distributed Tracing) extension
  when trace context is present on `LogRecord`s.
  ([#1073](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1073))

* Added support for sending common schema `ex` (Exception) extension when
  exception is present on `LogRecord`s.
  ([#1082](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1082))

* Added support for sending common schema `eventId` field when `EventId.Id` is
  non-zero on `LogRecord`s.
  ([#1127](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1127))

* Update OpenTelemetry to 1.5.1
  ([#1255](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1255))

## 0.1.0-alpha.2

Released 2023-Mar-6

* Update OpenTelemetry to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

* Tenant token is no longer exposed on `OneCollectorExporterOptions` and will be
  set automatically from the instrumentation key. Added new registration
  overloads and a builder to help with configuration.
  ([#1032](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1032))

* Switched to using a connection string design instead of passing
  instrumentation key directly.
  ([#1037](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1037))

* Added `RegisterPayloadTransmittedCallback` API on `OneCollectorExporter<T>`.
  ([#1058](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1058))

## 0.1.0-alpha.1

Released 2023-Feb-16

* Initial release.

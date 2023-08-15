# Changelog

## Unreleased

* Added support for receiving tranmission failures via the
  `RegisterPayloadTransmittedCallback` API.
  ([#1305](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1305))

* Added support for custom complex type serialization into JSON via the
  `OneCollectorExporterJsonSerializationOptions.RegisterFormatter` API.
  ([#1305](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1305))

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

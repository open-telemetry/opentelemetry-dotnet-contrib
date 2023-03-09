# Changelog

## Unreleased

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

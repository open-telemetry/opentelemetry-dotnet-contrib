# Changelog

## Unreleased

## 1.12.0-beta.1

Released 2025-Sep-10

* **Breaking change**: Renamed some extension methods from `AddTraceEnricher*()`
  to `TryAddTraceEnricer*()` pattern to more accurately reflect their behavior of
  only adding the enricher if it hasn't already been added.
  ([#3085](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3085))

## 1.12.0-alpha.1

Released 2025-Aug-18

* Updated OpenTelemetry core component version(s) to `1.12.0`.
  ([#2725](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2725))

## 0.1.0-alpha.2

Released 2025-Mar-05

* Updated OpenTelemetry core component version(s) to `1.11.2`.
  ([#2582](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2582))

## 0.1.0-alpha.1

Released 2025-Feb-07

* Make Extensions.Enrichment AoT compatible.
  ([#1541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1541))

* Drop support for .NET 6 as this target is no longer supported.
  ([#2126](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2126))

* Updated OpenTelemetry core component version(s) to `1.11.1`.
  ([#2477](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2477))

# Changelog

## Unreleased

* Update OpenTelemetry.Api to 1.4.0.
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-alpha.2

Released 2022-Nov-10

* Update OpenTelemetry.Api to 1.3.1.
* Change `EventCounter` prefix to `ec` and trim the event source name to keep
  instrument name under 63 characters.
  ([#740](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/740))

## 1.0.0-alpha.1

Released 2022-Oct-17

* Simplified implementation. EventSources must be explicitly configured to be
  listened to now.
  ([#620](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/620))

## 0.1.0-alpha.1

Released 2022-Jul-12

* This is the first release of `OpenTelemetry.Instrumentation.EventCounters` package.

For more details, please refer to the [README](README.md).

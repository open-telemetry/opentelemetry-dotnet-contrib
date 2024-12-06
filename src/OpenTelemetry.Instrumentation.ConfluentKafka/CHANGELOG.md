# Changelog

## Unreleased

* Drop support for .NET 6 as this target is no longer supported
  and add .NET Standard 2.0 target.
  ([#2142](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2142))

* Updated OpenTelemetry core component version(s) to `1.10.0`.
  ([#2317](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2317))

* Trace instrumentation will now call the [Activity.SetStatus](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.setstatus)
  API instead of the deprecated OpenTelemetry API package extension when setting
  span status. For details see: [Setting Status](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md#setting-status).
  ([#2358](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2358))

## 0.1.0-alpha.2

Released 2024-Sep-18

* Add named instrumentation support
  ([#2074](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2074))

## 0.1.0-alpha.1

Released 2024-Sep-16

* Initial release

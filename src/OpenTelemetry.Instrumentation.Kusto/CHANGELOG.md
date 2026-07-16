# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.17.0`.
  ([#4773](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4773))

## 0.1.0-alpha.2

Released 2026-Jul-03

* Assemblies are now digitally signed using cosign.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4637))

## 0.1.0-alpha.1

Released 2026-Jul-02

* Initial implementation.
  ([#3591](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3591))

* Recognized the `EXC_CTOR.<ExceptionType>` exception source id introduced in
  `Microsoft.Azure.Kusto.Data` 14.1.2 so `error.type` continues to be recorded
  for failed queries.
  ([#4630](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4630))

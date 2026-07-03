# Changelog

## Unreleased

## 0.1.0-alpha.1

Released 2026-Jul-02

* Initial implementation.
  ([#3591](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3591))

* Recognized the `EXC_CTOR.<ExceptionType>` exception source id introduced in
  `Microsoft.Azure.Kusto.Data` 14.1.2 so `error.type` continues to be recorded
  for failed queries.
  ([#4630](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4630))

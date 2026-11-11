# Changelog

## Unreleased

* **Breaking Change** Dropped support for `net8.0` and `net9.0`.
  ([#3867](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867))

* Add support for .NET 11.0.
  ([#3867](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867))

* Updated OpenTelemetry core component version(s) to `1.21.0`.
  ([#5969](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5969))

## 0.1.0-alpha.8

Released 2026-Nov-09

* Updated Semantic Conventions version to `1.45.0`.
  ([#5833](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5833))

* **Breaking change**: The `server.port` attribute is no longer recorded when the
  port is the default port for the scheme of the cluster URI (for example `443`
  for `https`), to follow the v1.45.0 database semantic conventions.
  ([#5833](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5833))

* Fixed query text sanitization taking quadratic time for queries with many
  signed numeric literals when `RecordQueryText` is enabled.
  ([#5937](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5937))

* Fixed excessive CPU usage when generating `db.query.summary` for queries
  containing a very large number of `materialized_view()` calls.
  ([#5938](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5938))

* Fixed literal values not being sanitized in `db.query.text` for control
  commands the Kusto grammar does not recognize (such as `.list blobs`).
  ([#5936](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5936))

* Fixed failed operations exporting the error message returned by the cluster as
  the span status description. The message is now only recorded as an exception
  event when the new `RecordException` option is enabled.
  ([#5911](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5911))

* Fixed a client request id or database name containing `text=` being able to
  bypass `db.query.text` sanitization and set `db.query.summary`.
  ([#5913](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5913))

* Fixed `db.query.summary` generation consuming excessive CPU and memory for
  some queries. Table names are now identified from the query syntax, so
  `db.query.summary` may differ for some queries (for example, column names
  are no longer included).
  ([#5912](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5912))

## 0.1.0-alpha.7

Released 2026-Oct-21

* Updated OpenTelemetry core component version(s) to `1.20.1`.
  ([#5651](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5651))

## 0.1.0-alpha.6

Released 2026-Oct-15

* Updated OpenTelemetry core component version(s) to `1.20.0`.
  ([#5541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5541))

## 0.1.0-alpha.5

Released 2026-Sep-18

* Updated OpenTelemetry core component version(s) to `1.19.0`.
  ([#5240](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5240))

## 0.1.0-alpha.4

Released 2026-Aug-21

* Updated OpenTelemetry core component version(s) to `1.18.0`.
  ([#5022](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5022))

## 0.1.0-alpha.3

Released 2026-Jul-17

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

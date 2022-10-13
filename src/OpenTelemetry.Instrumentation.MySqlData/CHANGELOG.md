# Changelog

## Unreleased

* Update OTel API version to `1.3.1`.
  ([#631](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/631))
* Compatibility with `Mysql.Data` 8.0.31.

## 1.0.0-beta.3

Released 2022-Jun-29

* Fix incomplete db.statement when the length>300
  [(#424)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/424)

## 1.0.0-beta.2

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.MySqlData`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.MySqlData).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.MySqlData`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.MySqlData)
  [(#256)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/256)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.MySqlData` -> `using
    OpenTelemetry.Instrumentation.MySqlData`)

## 1.0.0-beta1

* This is the first release of `OpenTelemetry.Contrib.Instrumentation.MySqlData`
  package.

For more details, please refer to the [README](README.md).

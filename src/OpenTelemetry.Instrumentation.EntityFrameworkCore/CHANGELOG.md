# Changelog

## Unreleased

* Added `Filter` public API on `EntityFrameworkInstrumentationOptions` to
  enable filtering of instrumentation.
  ([#1203](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1203))

* Updated OpenTelemetry SDK package version to 1.6.0
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

* Fix issue of multiple instances of OpenTelemetry-Instrumentation EventSource
  being created
  ([#1362](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1362))

## 1.0.0-beta.7

Released 2023-Jun-09

* Updated OpenTelemetry SDK package version to 1.5.0
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 1.0.0-beta.6

Released 2023-Mar-13

* Added overloads which accept a name to the `TracerProviderBuilder`
  `EntityFrameworkInstrumentationOptions` extension to allow for more fine-grained
  options management
   ([#1020](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1020))

## 1.0.0-beta.5

Released 2023-Feb-27

* Updated OpenTelemetry SDK package version to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-beta.4

Released 2023-Jan-25

* Updated OpenTelemetry SDK package version to 1.3.2
  ([#917](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/917))

* Update the `ActivitySource` name used to the assembly name:
`OpenTelemetry.Instrumentation.EntityFrameworkCore`
  ([#486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/486))

* Removes `AddEntityFrameworkCoreInstrumentation` method with default configure
  parameter.
  ([#916](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/916))

* Added support to `EnrichWithIDbCommand`
  ([#868](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/868))

* Map missing dbs to db.system:
`OpenTelemetry.Instrumentation.EntityFrameworkCore`
  [#898](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/898)

## 1.0.0-beta.3

Released 2022-Mar-18

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.EntityFrameworkCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EntityFrameworkCore).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore)
  ([#261](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/261))

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore` -> `using
    OpenTelemetry.Instrumentation.EntityFrameworkCore`)

## 1.0.0-beta2

Released 2021-Jun-11

* EntityFrameworkCore instrumentation to depend on API and not SDK
  ([#121](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/121))

## 0.6.0-beta

Released 2020-Sep-29

* This is the first release of
  `OpenTelemetry.Contrib.Instrumentation.EntityFrameworkCore` package.

For more details, please refer to the [README](README.md).

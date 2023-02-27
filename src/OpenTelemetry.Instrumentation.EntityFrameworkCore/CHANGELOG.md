# Changelog

## Unreleased

* Updated OTel SDK package version to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-beta.4

Released 2023-Jan-25

* Updated OTel SDK package version to 1.3.2
  ([#917](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/917))

* Update the `ActivitySource` name used to the assembly name:
`OpenTelemetry.Instrumentation.EntityFrameworkCore`
  ([#486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/486))

* Removes `AddEntityFrameworkCoreInstrumentation` method with default configure
  parameter.
  ([#916](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/916))

* Added support to `EnrichWithIDbCommand`
  ([#868](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/868))

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

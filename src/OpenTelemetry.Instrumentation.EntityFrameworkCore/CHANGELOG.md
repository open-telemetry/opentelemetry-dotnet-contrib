# Changelog

## Unreleased

* The new database semantic conventions can be opted in to by setting
  the `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. This allows for a
  transition period for users to experiment with the new semantic conventions
  and adapt as necessary. The environment variable supports the following
  values:
  * `database` - emit the new, frozen (proposed for stable) database
  attributes, and stop emitting the old experimental database
  attributes that the instrumentation emitted previously.
  * `database/dup` - emit both the old and the frozen (proposed for stable) database
  attributes, allowing for a more seamless transition.
  * The default behavior (in the absence of one of these values) is to continue
  emitting the same database semantic conventions that were emitted in
  the previous version.
  * Note: this option will be be removed after the new database
  semantic conventions is marked stable. At which time this
  instrumentation can receive a stable release, and the old database
  semantic conventions will no longer be supported. Refer to the
  specification for more information regarding the new database
  semantic conventions for
  [spans](https://github.com/open-telemetry/semantic-conventions/blob/v1.28.0/docs/database/database-spans.md).
  ([#2130](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2130))

## 1.0.0-beta.12

Released 2024-Jun-18

* Update `Microsoft.Extensions.Options` to `8.0.0`.
  ([#1830](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1830))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 1.0.0-beta.11

Released 2024-Apr-05

* Update OpenTelemetry SDK version to `1.8.0`.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 1.0.0-beta.10

Released 2024-Feb-07

* **Breaking Change**: Stop emitting `db.statement_type` attribute.
  This attribute never was part of the [semantic convention](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/database/database-spans.md#call-level-attributes).
  ([#1559](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1559))

* `ActivitySource.Version` is set to NuGet package version.
  ([#1624](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1624))

## 1.0.0-beta.9

Released 2024-Jan-03

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

## 1.0.0-beta.8

Released 2023-Oct-24

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

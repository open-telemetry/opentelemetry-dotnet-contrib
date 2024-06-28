# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 1.0.0-beta.5

Released 2024-May-08

* Add LogToActivityEventConversionOptions.Filter callback
  ([#1059](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1059))

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

* Add Baggage Activity Processor.
  ([#1659](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1659))

## 1.0.0-beta.4

Released 2023-Feb-27

* Update OpenTelemetry to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-beta.3

Released 2022-Nov-09

* Update OpenTelemetry to 1.4.0-beta.2
  ([#680](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/680))

* Implemented auto flush activity processor
  ([#297](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/297))

* Removes .NET Framework 4.6.1. The minimum .NET Framework version
  supported is .NET 4.6.2.

* Removes net5.0 target as .NET 5.0 is going out
  of support. The package keeps netstandard2.0 target, so it
  can still be used with .NET5.0 apps.
  ([#617](https://github.com/open-telemetry/opentelemetry-dotnet/pull/617))

* Going forward the NuGet package will be
  [`OpenTelemetry.Extensions`](https://www.nuget.org/packages/OpenTelemetry.Extensions).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Preview`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Preview)
  [(#266)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/266)

## 1.0.0-beta2

* This is the first release of `OpenTelemetry.Contrib.Preview` package.

For more details, please refer to the [README](README.md).

# Changelog

## Unreleased

* `ActivitySource.Version` is set to NuGet package version.
  ([#1624](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1624))

* Update `Microsoft.Extensions.Options` to `8.0.0`.
  ([#1830](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1830))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 1.6.0-beta.1

Released 2023-Dec-20

* Update `OpenTelemetry.Api.ProviderBuilderExtensions` to `1.6.0`.
  * Update `OpenTelemetry.Api` to `1.6.0`.
  ([#1502](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1502))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `HangfireInstrumentationOptions` extension to allow for more fine-grained
  options management
  ([#1442](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1442))

* Add Filter to HangfireInstrumentationOptions.
  ([#1440](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1440))

## 1.5.0-beta.1

Released 2023-Jun-23

* Update OTel API version to `1.5.0`.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

* Removes `AddHangfireInstrumentation` method with default configure default parameter.
  ([#1129](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1129))

* Support Hangfire `1.8`.
  ([#1202](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1202))

## 1.0.0-beta.4

Released 2022-Dec-15

* Add support for custom job display names
  ([#756](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/756))

## 1.0.0-beta.3

Released 2022-Oct-26

* Add support to optionally record exceptions
  ([#719](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/719))

* Update OTel API version to `1.3.1`.
  ([#631](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/631))

## 1.0.0-beta.2

Released 2022-Jul-14

* Added client side instrumentation for jobs
  ([#421](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/421))

## 1.0.0-beta.1

Released 2022-Jun-03

* Updated OTel API package version to `1.2.0`
  ([#353](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/353))

## Initial Release

* This is the first release of `OpenTelemetry.Instrumentation.Hangfire` package.

For more details, please refer to the [README](README.md).

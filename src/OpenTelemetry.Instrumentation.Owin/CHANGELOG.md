# Changelog

## Unreleased

* Updated OpenTelemetry SDK to 1.6.0
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))
* Removes `AddOwinInstrumentation` method with default configure parameter.
  ([#929](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/929))
* Adds HTTP server metrics via `AddOwinInstrumentation` extension method on `MeterProviderBuilder`
  ([#1335](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1335))

## 1.0.0-rc.3

Released 2022-Sep-20

* Changed activity source name from `OpenTelemetry.OWIN`
  to `OpenTelemetry.Instrumentation.Owin`
  ([#572](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/572))
* Changed to depend on at least Owin 4.2.2 to resolve a
  [denial of service vulnerability](https://github.com/advisories/GHSA-3rq8-h3gj-r5c6).
  ([#648](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/648))
* Updated project to target `net462` and OTel 1.3.1 SDK
  ([#653](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/653))

## 1.0.0-rc.2

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.Owin`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.Owin`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Owin)
  [(#257)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/257)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.Owin` -> `using
    OpenTelemetry.Instrumentation.Owin`)

## 1.0.0-rc1

* This is the first release of `OpenTelemetry.Contrib.Instrumentation.Owin` package.

For more details, please refer to the [README](README.md).

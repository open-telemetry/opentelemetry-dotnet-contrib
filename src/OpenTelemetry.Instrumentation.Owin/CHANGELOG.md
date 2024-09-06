# Changelog

## Unreleased

* Updated activity tags to use new
  [semantic conventions](https://github.com/open-telemetry/semantic-conventions/tree/v1.27.0/docs/http/http-spans.md)
  attribute schema.
  ([#2028](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2028))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

* Updated registration extension code to retrieve environment variables through
  `IConfiguration`.
  ([#1973](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1973))

* **Breaking change** Updated to depend on the
  `OpenTelemetry.Api.ProviderBuilderExtensions` (API) package instead of the
  `OpenTelemetry` (SDK) package.
  ([#1977](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1977))

## 1.0.0-rc.6

Released 2024-Apr-19

* Massive memory leak in OwinInstrumentationMetrics addressed.
  Made both Meter and Histogram singletons.
  ([#1655](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1655))

## 1.0.0-rc.5

Released 2024-Apr-17

* **Breaking Change**: Fixed tracing instrumentation so that by default any
  values detected in the query string component of requests are replaced with
  the text `Redacted` when building the `http.url` tag. For example,
  `?key1=value1&key2=value2` becomes `?key1=Redacted&key2=Redacted`. You can
  disable this redaction by setting the environment variable
  `OTEL_DOTNET_EXPERIMENTAL_OWIN_DISABLE_URL_QUERY_REDACTION` to `true`.
  ([#1656](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1656))

* `ActivitySource.Version` and `Meter.Version` are set to NuGet package version.
  ([#1624](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1624))

* Updated OpenTelemetry SDK to 1.8.0.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 1.0.0-rc.4

Released 2024-Mar-20

* Updated OpenTelemetry SDK to 1.7.0.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

* Removes `AddOwinInstrumentation` method with default configure parameter.
  ([#929](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/929))

* Adds HTTP server metrics via `AddOwinInstrumentation` extension method on `MeterProviderBuilder`
  ([#1335](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1335))

* Fix description for `http.server.request.duration` metric.
  ([#1538](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1538))

* Span status is set based on [semantic convention for server spans](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md#status).
  ([#1538](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1538))

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

# Changelog

## Unreleased

* Changed activity source name from `OpenTelemetry.OWIN`
  to `OpenTelemetry.Instrumentation.Owin`
  ([#572](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/572))

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

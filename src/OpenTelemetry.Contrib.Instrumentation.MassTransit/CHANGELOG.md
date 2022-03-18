# Changelog

## Unreleased

## 1.0.0-beta.3

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.MassTransit`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.MassTransit).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.MassTransit`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.MassTransit)
  [(#248)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/248)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.MassTransit` -> `using
    OpenTelemetry.Instrumentation.MassTransit`)

## 1.0.0-beta2

Released 2021-June-17

* Updated OTel SDK package version to 1.1.0-beta4
  ([#136](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/136))

## Initial Release

* Updated OTel SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

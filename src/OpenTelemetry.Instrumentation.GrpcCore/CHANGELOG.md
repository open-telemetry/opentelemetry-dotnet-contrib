# Changelog

## Unreleased

* Make the context propagation extraction case insensitive.
  ([#483](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/483))
* Update OpenTelemetry.Api to 1.6.0.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

## 1.0.0-beta.5

* Switched Grpc.Core package dependency to Grpc.Core.Api in the same range.
  No functional change, just less exposure to unnecessary packages.

## 1.0.0-beta.4

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.GrpcCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcCore).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.GrpcCore`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.GrpcCore)
  [(#255)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/255)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.GrpcCore` -> `using
    OpenTelemetry.Instrumentation.GrpcCore`)

## 1.0.0-beta3

* Updated OpenTelemetry SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

* Do NOT mutate incoming call headers, copy them before propagation
  ([#143](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/143))

## 1.0.0-beta2

* This is the first release of `OpenTelemetry.Contrib.Instrumentation.GrpcCore`
  package.

For more details, please refer to the [README](README.md).

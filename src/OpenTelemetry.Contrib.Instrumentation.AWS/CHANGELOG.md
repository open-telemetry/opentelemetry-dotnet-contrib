# Changelog - OpenTelemetry.Contrib.Instrumentation.AWS

## Unreleased

* Raised the minimum .NET version to `net462`
  ([#1095](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1095))
* Removes `AddAWSInstrumentation` method with default configure default parameter.
  ([#1117](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1117))

## 1.0.2

Released 2022-Nov-11

* Fixed issue when using version 3.7.100 of the AWS SDK for .NET triggering an
  EndpointResolver not found exception.
  ([#726](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/726))

## 1.0.1

Released 2021-Feb-24

This is the first release for the `OpenTelemetry.Contrib.Instrumentation.AWS`
project. The release targets v1.0.1 of the
[OpenTelemetry.Contrib.Extensions.AWSXRay](https://www.nuget.org/packages/OpenTelemetry.Contrib.Extensions.AWSXRay/)

For more details, please refer to the
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Contrib.Instrumentation.AWS/README.md)

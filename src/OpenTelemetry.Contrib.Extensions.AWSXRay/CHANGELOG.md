# Changelog - OpenTelemetry.Contrib.Extensions.AWSXRay

## Unreleased

* Updated OTel SDK package version to 1.2.0
* Update minimum support to net462

## 1.1.0 [2021-Sept-20]

* Added AWS resource detectors ([#149](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/149))
* Updated OTel SDK package version to 1.1.0
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

## 1.0.1 [2021-Feb-24]

This is the first release for the `OpenTelemetry.Contrib.Extensions.AWSXRay`
project. The project targets v1.0.1 of the [OpenTelemetry
SDK](https://www.nuget.org/packages/OpenTelemetry/).

The AWSXRay extensions include plugin to generate X-Ray format trace-ids and a
propagator to propagate the X-Ray trace header to downstream. For more details,
please refer to the
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Contrib.Extensions.AWSXRay/README.md)

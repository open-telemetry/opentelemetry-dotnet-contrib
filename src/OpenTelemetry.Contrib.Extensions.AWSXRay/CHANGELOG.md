# Changelog - OpenTelemetry.Contrib.Extensions.AWSXRay

## 1.3.0 [TBD]

* Enhancement - AWSXRayIdGenerator - Generate X-Ray IDs with global Random
  instance instead of recreating with ThreadLocal
  ([#380](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/380))

## 1.2.0 [2022-May-18]

* Enhancement - AWSEKSResourceDetector - Validate ClusterName/ContainerID
  independently before adding it to the resource
  ([#205](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/205))
* Fix - AWSEKSResourceDetector fails to detect resources due to exception
  "The SSL connection could not be established"
  ([#208](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/208))

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

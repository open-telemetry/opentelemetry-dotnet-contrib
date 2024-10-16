# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

* Drop support for .NET 6 as this target is no longer supported
  and add .NET Standard 2.0 target.
  ([#2164](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2164))

* Bumped the `System.Text.Json` reference to `6.0.10` for runtimes older than
  `net8.0` and added a direct reference for `System.Text.Json` at `8.0.5` on
  `net8.0` in response to
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#2196](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2196))

## 1.5.0-beta.1

Released 2024-Jun-04

* Implement support for cloud.{account.id,availability_zone,region} attributes in
  AWS ECS detector.
  ([#1552](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1552))

* Implement support for `cloud.resource_id` attribute in AWS ECS detector.
  ([#1576](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1576))

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

* **Breaking Change**: Renamed package from `OpenTelemetry.ResourceDetectors.AWS`
  to `OpenTelemetry.Resources.AWS`.
  ([#1839](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1839))

* **Breaking Change**: `AWSEBSResourceDetector`, `AWSEC2ResourceDetector`,
`AWSECSResourceDetector` and `AWSEKSResourceDetector` types are now internal,
use `ResourceBuilder` extension methods `AddAWSEBSDetector`,
`AddAWSEC2Detector`, `AddAWSECSDetector`
and `AddAWSEKSDetector` respectively to enable the detectors.
  ([#1839](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1839))

* **Breaking Change**: Renamed EventSource
from `OpenTelemetry-ResourceDetectors-AWS`
to `OpenTelemetry-Resources-AWS`.
  ([#1839](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1839))

## 1.4.0-beta.1

Released 2024-Jan-26

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

* Fix AWS EBS Resource Detector working on linux.
  ([#1350](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1350))

* BREAKING: All Resource Detector classes marked as `sealed`.
  ([#1510](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1510))

* Make OpenTelemetry.ResourceDetectors.AWS native AoT compatible.
  ([#1541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1541))

## 1.3.0-beta.1

Released 2023-Aug-02

* Initial release. Previously it was part of `OpenTelemetry.Contrib.Extensions.AWSXRay`
  package.
  ([#1140](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1140))

* Update OpenTelemetry SDK version to `1.5.1`.
  ([#1255](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1255))

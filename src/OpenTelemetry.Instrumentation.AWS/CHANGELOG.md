# Changelog - OpenTelemetry.Instrumentation.AWS

## Unreleased

## 1.1.0-beta.6

Released 2024-Sep-10

* Fix Memory Leak by Reusing ActivitySources, Meters, and Instruments
  ([#2039](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2039))
* Added instrumentation support for AWS Bedrock, BedrockRuntime, BedrockAgent, BedrockAgentRuntime.
  ([#1979](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1979))

## 1.1.0-beta.5

Released 2024-Aug-22

* BREAKING: Update the instrumentation logic to use AWS TracerProvider.
  ([#1974](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1974))
* Add AWS metrics instrumentation.
  ([#1980](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1980))
* Updated dependency on AWS .NET SDK to version 3.7.400.
  ([#1974](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1980))
* Added `rpc.system`, `rpc.service`, and `rpc.method` to activity tags based on
  [semantic convention v1.26.0](https://github.com/open-telemetry/semantic-conventions/blob/v1.26.0/docs/cloud-providers/aws-sdk.md#common-attributes).
  ([#1865](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1865))

## 1.1.0-beta.4

Released 2024-Apr-12

* BREAKING: Switched AWSServiceName tag to use ServiceId instead of ServiceName
  ([#1572](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1572))
* `ActivitySource.Version` is set to NuGet package version.
  ([#1624](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1624))

## 1.1.0-beta.3

Released 2024-Jan-26

* Updated dependency on AWS .NET SDK to version 3.7.300.
  ([#1542](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1542))
* Add target for `net6.0`.
  ([#1547](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1547))
* Add support for native AoT.
  ([#1547](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1547))

## 1.1.0-beta.2

Released 2023-Dec-01

* Updated dependency on AWS .NET SDK to version 3.7.100.
  ([#1454](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1454))

## 1.1.0-beta.1

Released 2023-Aug-07

* BREAKING (renaming): renamed `OpenTelemetry.Contrib.Instrumentation.AWS` to `OpenTelemetry.Instrumentation.AWS`
  ([#1275](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1275))
* Raised the minimum .NET Framework version to `net462`
  ([#1095](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1095))
* Removes `AddAWSInstrumentation` method with default configure default parameter.
  ([#1117](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1117))
* Global propagator is now used to inject into sent SQS and SNS message
  attributes (in addition to X-Ray propagation).
  ([#1051](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1051))
* Change dependency from `OpenTelemetry.Contrib.Extensions.AWSXRay` to `OpenTelemetry.Extensions.AWS`
  ([#1288](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1288))

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
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AWS/README.md)

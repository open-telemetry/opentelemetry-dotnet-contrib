# Changelog - OpenTelemetry.Instrumentation.AWSLambda

## Unreleased

* Add detection of Lambda cold start and set `faas.coldstart` Activity tag.
  ([#2037](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2037))
* Add HTTP server span attributes for Application Loadbalancer triggers
  ([#2033](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2033))

## 1.3.0-beta.1

Released 2024-Jan-26

* BREAKING: `ILambdaContext context` argument of all tracing methods of
  `OpenTelemetry.Instrumentation.AWSLambda.AWSLambdaWrapper` was annotated as non-nullable.
* Enabled null state analysis for `OpenTelemetry.Instrumentation.AWSLambda`.
  The interface will now contain attributes for null-state static analysis.
  If null state analysis is enabled in your depending project, you may encounter
  new warnings.
  ([#1295](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1295))
* BREAKING: Target `net6.0` instead of `netstandard2.0`
  ([#1545](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1545))
* Add support for native AoT.
  `Amazon.Lambda.*` NuGet package dependencies have been upgraded, see package
  dependencies for details.
  ([#1544](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1544))

## 1.2.0-beta.1

Released 2023-Aug-07

* BREAKING: `AddAWSLambdaConfigurations` no longer removes all existing
  resource attributes
* BREAKING: Change dependency from `OpenTelemetry.Contrib.Extensions.AWSXRay`
  to `OpenTelemetry.Extensions.AWS`
  ([#1289](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1289)).
  This now requires at least OpenTelemetry 1.5.1.
* Add explicit dependency on Newtonsoft.Json, upgrading the minimum version.

  This resolves a warning that some dependency analyzers may produce where this
  package would transitively depend on a vulnerable version of Newtonsoft.Json
  through [Amazon.Lambda.APIGatewayEvents][].

  This also avoids a potential issue where the instrumentation would try to call
  a Newtonsoft.Json function when no other package nor the app itself depends on
  Newtonsoft.Json, since the transitive dependency would be ignored unless using
  application were compiled against a TargetFramework older than Core 3.1.

[Amazon.Lambda.APIGatewayEvents]: https://www.nuget.org/packages/Amazon.Lambda.APIGatewayEvents/2.4.1#dependencies-body-tab

## 1.1.0-beta.3

Released 2023-Jun-13

* Add HTTP server span attributes for API Gateway triggers
  ([#626](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/626))
* Removes `AddAWSLambdaConfigurations` method with default configure parameter.
  ([#943](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/943))
* BREAKING (behavior): `AddAWSLambdaConfigurations` no longer calls `AddService`
  ([#1080](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1080))
* Added tracing of AWS Lambda handlers receiving SQS and SNS messages.
  ([#1051](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1051))

## 1.1.0-beta.2

Released 2022-Sep-14

Release PR: [#590](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/590)
& [#639](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/639).

This is the first release with the new package name `OpenTelemetry.Instrumentation.AWSLambda`.

* BREAKING (API, behavior): Rename package to `OpenTelemetry.Instrumentation.AWSLambda`
  (remove `.Contrib`) ([#593](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/593)).
  This also affects the `ActivitySource` name (superseding [#534](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/534)).
* Pre-release version numbering scheme changed from `.betaN` to `beta.N` ([#639](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/639))
* BREAKING (API): Move public class `AWSLambdaWrapper` out of `Implementation` subnamespace
  ([#593](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/593))
* BREAKING (API): Rename overloads of `AWSLambdaWrapper.Trace` that take an async
  handler to `TraceAsync`, to emphasize that they (usually) need to be awaited.
  ([#608](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/608))
* Rewrite of parent context handling and related changes
  ([#408](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/408)):
  * BREAKING (API): Remove `AWSLambdaWrapper.Trace`/`TraceAsync` overloads
    without `ILambdaContext` parameter.
  * BREAKING (behavior): Add automatic parent extraction from HTTP triggers
    (API Gateway Proxy events), using the configured global textmap propagator.
  * BREAKING (behavior): An activity is now also created if no parent context
    could be extracted (previously this package would only create activities if
    a valid parent span context could be extracted with X-Ray).
  * Add optional parent context (`ActivityContext`) to `AWSLambdaWrapper.Trace`/`TraceAsync`.
  * Add `AWSLambdaInstrumentationOptions.DisableAwsXRayContextExtraction`
    initialization option.
* Add version to `ActivitySource` ([#593](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/593))

## 1.1.0-beta1

Released 2021-May-26

This is the first release for the `OpenTelemetry.Contrib.Instrumentation.AWSLambda`
project. The project targets v1.1.0-beta1 of the [OpenTelemetry
SDK](https://www.nuget.org/packages/OpenTelemetry/).

The AWSLambda library includes extension and tracing APIs to configure resource detector
and generate incoming AWS Lambda OTel span. For more details, please refer to the
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AWSLambda-1.1.0-beta1/src/OpenTelemetry.Contrib.Instrumentation.AWSLambda/README.md)

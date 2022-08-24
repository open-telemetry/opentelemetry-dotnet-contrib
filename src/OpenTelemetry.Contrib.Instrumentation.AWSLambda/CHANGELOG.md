# Changelog - OpenTelemetry.Contrib.Instrumentation.AWSLambda

## Unreleased

## 2.0.0-beta1

* BREAKING (behavior): Update the `ActivitySource` name to not include `Contrib`.
  The new activity source name is `OpenTelemetry.Instrumentation.AWSLambda`
  ([#534](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/534))
* Rewrite of parent context handling and related changes
  ([#408](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/408)):
  * BREAKING (API): Remove `AWSLambdaWrapper.Trace` overloads
    without `ILambdaContext` parameter.
  * BREAKING (behavior): Add automatic parent extraction from HTTP triggers
  * (API Gateway Proxy events), using the configured global textmap propagator.
  * BREAKING (behavior): An activity is now also created if no parent context
    could be extracted (previously this package would only create activities if
    a valid parent span context could be extracted with X-Ray).
  * Add optional parent context (`ActivityContext`) to `AWSLambdaWrapper.Trace`.
  * Add `AWSLambdaInstrumentationOptions.DisableAwsXRayContextExtraction`
    initialization option.
  * Add `AWSLambdaWrapper.Trace` overloads without generic input arguments.

## 1.1.0-beta1

Released 2021-May-26

This is the first release for the `OpenTelemetry.Contrib.Instrumentation.AWSLambda`
project. The project targets v1.1.0-beta1 of the [OpenTelemetry
SDK](https://www.nuget.org/packages/OpenTelemetry/).

The AWSLambda library includes extension and tracing APIs to configure resource detector
and generate incoming AWS Lambda OTel span. For more details, please refer to the
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Contrib.Instrumentation.AWSLambda/README.md)

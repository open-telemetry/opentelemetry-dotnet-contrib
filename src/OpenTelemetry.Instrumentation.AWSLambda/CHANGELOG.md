# Changelog - OpenTelemetry.Instrumentation.AWSLambda

## Unreleased

* Breaking change: Rename package to OpenTelemetry.Instrumentation.AWSLambda (remove ".Contrib")
* Breaking change: Move public class AWSLambdaWrapper out of Implementation subnamespace.
* Add version to ActivitySource
* Updated the `ActivitySource` name to the assembly name:
  `OpenTelemetry.Instrumentation.AWSLambda`
  ([#534](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/534))
* Added public option `AWSLambdaInstrumentationOptions.DisableAwsXRayContextExtraction`.
* Extended public API of the `AWSLambdaWrapper`: added optional parent
  context (`ActivityContext`) to all `Trace` methods.
* Enhanced parent extraction: if the parent context is not provided
  then it can be extracted from the incoming request for certain types of the request.
  If the parent is not extracted from the incoming request then it can be extracted
  from the AWS X-Ray tracing header if AWS X-Ray context extraction
  is not disabled (`DisableAwsXRayContextExtraction`).
* Changed behaviour of the `OnFunctionStart` method: Activity is created even
  if the parent context is not defined.
* Breaking change: `AWSLambdaWrapper.Trace` overloads without `ILambdaContext` argument
  have been completely removed.
* Added two new `AWSLambdaWrapper.Trace` overloads without generic input arguments.
  ([#408](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/408))

## 1.1.0-beta1

Released 2021-May-26

This is the first release for the `OpenTelemetry.Contrib.Instrumentation.AWSLambda`
project. The project targets v1.1.0-beta1 of the [OpenTelemetry
SDK](https://www.nuget.org/packages/OpenTelemetry/).

The AWSLambda library includes extension and tracing APIs to configure resource detector
and generate incoming AWS Lambda OTel span. For more details, please refer to the
[README](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Contrib.Instrumentation.AWSLambda/README.md)

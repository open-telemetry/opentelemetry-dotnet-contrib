# Changelog

## Unreleased

* Fix issue of multiple instances of OpenTelemetry-Instrumentation EventSource
  being created
  ([#1362](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1362))
* Updated OpenTelemetry SDK package version to 1.6.0
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

## 1.0.0-beta.4

Released 2023-Mar-06

* Updated OpenTelemetry SDK package version to 1.4.0
  ([#1019](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1019))
* Update minimum full framework support to net462
* Requests that get an HTTP status code of 404 are not marked as an error span status
* Add MaxDbStatementLength option with default of 4096
* Remove duplicated HTTP method and URL from db.statement attribute value
* Fix faulty logic of MaxDbStatementLength option
  ([#425](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/425))
* Remove method with default attribute
  ([#1019](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1019))
* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddElasticsearchClientInstrumentation` extension to allow for more fine-grained
  options management
  ([#1019](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1019))

## 1.0.0-beta.3

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.ElasticsearchClient`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient)
  [(#248)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/248)

  Migration:

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient` -> `using
    OpenTelemetry.Instrumentation.ElasticsearchClient`)

## 1.0.0-beta2

Released 2021-June-17

* Updated OpenTelemetry SDK package version to 1.1.0-beta4
  ([#136](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/136))

## Initial Release

* Updated OpenTelemetry SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

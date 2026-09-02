# Changelog

## Unreleased

* Added support for registering custom Service Fabric exception convertors, so
  that applications can propagate their own exception types across remoting
  calls while still being instrumented. Service Fabric SDK 8 (runtime 11) no
  longer enables the `BinaryFormatter` fallback for exception serialization by
  default, and SDK 9 removes it altogether, so an exception without a
  registered convertor reaches the client as a `ServiceException`.
  `TraceContextEnrichedServiceRemotingProviderAttribute` and
  `TraceContextEnrichedActorRemotingProviderAttribute` are no longer sealed and
  expose `GetServiceExceptionConvertors()` / `GetClientExceptionConvertors()`
  for this purpose. A `RemotingExceptionDepth` property was also added to
  control how many levels of inner exceptions are serialized.

* Documented how to compose the instrumentation adapters manually, for
  applications that need to configure the Service Fabric listener or client
  factory directly instead of using the provider attributes.

## 1.18.0-beta.1

Released 2026-Aug-21

* Updated OpenTelemetry core component version(s) to `1.18.0`.
  ([#5022](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5022))

## 1.17.0-beta.1

Released 2026-Jul-17

* Assemblies are now digitally signed using cosign.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4637))

* Updated OpenTelemetry core component version(s) to `1.17.0`.
  ([#4773](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4773))

## 1.16.0-beta.1

Released 2026-Jun-16

* Raised the minimum required version of `Microsoft.ServiceFabric.Actors` and
  `Microsoft.ServiceFabric.Services.Remoting` from `7.1.2448` to `8.4.268`, as the
  `7.1` Service Fabric runtime is going out of support.
  ([#4510](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4510))

* Updated OpenTelemetry core component version(s) to `1.16.0`.
  ([#4487](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4487))

## 1.15.1-beta.1

Released 2026-Apr-21

* Ensure that `TransportSettings` configuration is applied to created
  instances of `IServiceRemotingClientFactory`.
  ([#4148](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4148))

* Updated OpenTelemetry core component version(s) to `1.15.3`.
  ([#4166](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4166))

## 1.15.0-beta.1

Released 2026-Jan-21

* Add `net8.0`, `net10.0`, and `net462` target frameworks.
  ([#3791](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3791))

* Updated OpenTelemetry core component version(s) to `1.15.0`.
  ([#3791](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3791))

## 1.14.0-beta.1

Released 2025-Nov-13

## 1.9.0-beta.1

Released 2024-Dec-24

* Initial release of `OpenTelemetry.Instrumentation.ServiceFabricRemoting` library.

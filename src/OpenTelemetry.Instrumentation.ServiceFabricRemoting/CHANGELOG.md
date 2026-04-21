# Changelog

## Unreleased

* Added RPC metrics instrumentation. Register via
  `MeterProviderBuilder.AddServiceFabricRemotingInstrumentation()` to emit
  `rpc.server.call.duration` and `rpc.client.call.duration` histograms per the
  OpenTelemetry RPC metrics semantic conventions.
  ([#4163](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4163))

## 1.15.1-beta.1

Released 2026-Apr-21

* Fixed a bug where `TransportSettings` configured in the service's
  `Config/Settings.xml` were ignored when using
  `TraceContextEnrichedServiceRemotingProviderAttribute`. Both
  `FabricTransportRemotingSettings` (client) and
  `FabricTransportRemotingListenerSettings` (listener) now load from the
  `TransportSettings` config section, with any attribute-level overrides
  (e.g. `MaxMessageSize`, `OperationTimeout`, `KeepAliveTimeout`,
  `ConnectTimeout`) layered on top of the loaded values.
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

# Changelog

## 1.0.0-rc6

* Fixed a `NullReferenceException` in
  `TelemetryDispatchMessageInspector.BeforeSendReply` when operation is OneWay
  ([#237](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/237))

## 1.0.0-rc5

* Fixed an `ArgumentNullException` setting `Activity`.`DisplayName` when
  processing service requests with empty actions
  ([#170](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/170))

## 1.0.0-rc4

* Removed `Propagator` property on `WcfInstrumentationOptions`
  ([#163](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/163))

## 1.0.0-rc3

* Added `TelemetryServiceBehavior`. **Breaking change** (config update
  required): Renamed `TelemetryBehaviourExtensionElement` ->
  `TelemetryEndpointBehaviorExtensionElement`
  ([#152](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/152))

* Added `TelemetryContractBehaviorAttribute` which can be used for programmatic
  configuration of WCF services & clients
  ([#153](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/153))

## 1.0.0-rc2

* Updated OTel SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

* Added enricher for WCF activity
  ([#126](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/126))

# Changelog

## Unreleased

* Update OpenTelemetry SDK version to `1.6.0`.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))
* Fixed span hierarchy when hosted in ASP.NET
  ([#1342](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1342))
* **Breaking Change** `TelemetryClientMessageInspector` and `TelemetryDispatchMessageInspector`
  changed from public to internal
  ([#1376](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1376))
* Added support for `IRequestSessionChannel` and `IDuplexChannel` channel shapes
  ([#1374](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1374))

## 1.0.0-rc.12

Released 2023-Aug-30

* Added support for non-SOAP requests.
  ([#1251](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1251))

## 1.0.0-rc.11

Released 2023-Aug-14

* Update OpenTelemetry SDK version to `1.5.1`.
  ([#1255](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1255))
* Client instrumentation implementation moved to lower-level `BindingElement`.
  ([#1247](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1247))

## 1.0.0-rc.10

Released 2023-Jun-09

* Update OpenTelemetry SDK version to `1.5.0`.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 1.0.0-rc.9

Released 2023-Feb-27

* Update OpenTelemetry SDK version to `1.4.0`.
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))
* Removes `AddWcfInstrumentation` method with default configure parameter.
  ([#928](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/928))

## 1.0.0-rc.8

Released 2022-Dec-28

* Update OpenTelemetry SDK version to `1.3.1`.
  ([#631](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/631))
* Change value `rpc.system` from `wcf` to `dotnet_wcf`.
  ([#837](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/837))

## 1.0.0-rc.7

Released 2022-Aug-23

* Updated OpenTelemetry SDK package version to 1.3.0
  ([#569](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/569))
* Changed activity source name from `OpenTelemetry.WCF`
  to `OpenTelemetry.Instrumentation.Wcf`
  ([#570](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/570))

## 1.0.0-rc.6

Released 2022-Mar-17

* Going forward the NuGet package will be
  [`OpenTelemetry.Instrumentation.Wcf`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Wcf).
  Older versions will remain at
  [`OpenTelemetry.Contrib.Instrumentation.Wcf`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Wcf)
  [(#247)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/247)

  Migration:

  * In config files update fully qualified references to not use "Contrib" (eg
    `type="OpenTelemetry.Contrib.Instrumentation.Wcf.TelemetryEndpointBehaviorExtensionElement,
    OpenTelemetry.Contrib.Instrumentation.Wcf"` ->
    `type="OpenTelemetry.Instrumentation.Wcf.TelemetryEndpointBehaviorExtensionElement,
    OpenTelemetry.Instrumentation.Wcf"`)

  * In code update namespaces (eg `using
    OpenTelemetry.Contrib.Instrumentation.Wcf` -> `using
    OpenTelemetry.Instrumentation.Wcf`)

* The minimum supported .NET Framework version is now .NET Framework 4.6.2.
  Previous versions will be going out of support in [April
  2022](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework)
  [(#247)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/247)

* Fixed a `NullReferenceException` in
  `TelemetryDispatchMessageInspector.BeforeSendReply` when operation is OneWay
  ([#237](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/237))

## 1.0.0-rc5

Released 2022-Feb-05

* Fixed an `ArgumentNullException` setting `Activity`.`DisplayName` when
  processing service requests with empty actions
  ([#170](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/170))

## 1.0.0-rc4

Released 2021-Oct-22

* Removed `Propagator` property on `WcfInstrumentationOptions`
  ([#163](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/163))

## 1.0.0-rc3

Released 2021-Sep-13

* Added `TelemetryServiceBehavior`. **Breaking change** (config update
  required): Renamed `TelemetryBehaviourExtensionElement` ->
  `TelemetryEndpointBehaviorExtensionElement`
  ([#152](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/152))

* Added `TelemetryContractBehaviorAttribute` which can be used for programmatic
  configuration of WCF services & clients
  ([#153](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/153))

## 1.0.0-rc2

Released 2021-Jun-16

* Updated OpenTelemetry SDK package version to 1.1.0-beta1
  ([#100](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/100))

* Added enricher for WCF activity
  ([#126](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/126))

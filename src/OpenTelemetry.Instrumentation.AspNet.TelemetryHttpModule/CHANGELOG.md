# Changelog

## Unreleased

* Update `OpenTelemetry.Api` to `1.6.0`.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

## 1.0.0-rc9.9

Released 2023-Jun-09

* Update `OpenTelemetry.Api` to `1.5.0`.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 1.0.0-rc9.8

Released 2023-Feb-27

* Update `OpenTelemetry.Api` to `1.4.0`.
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 1.0.0-rc9.7

Released 2022-Nov-28

* Restore Activity.Current before all IIS Lifecycle events
  ([#761](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/761))

## 1.0.0-rc9.6

Released 2022-Sep-28

* Update `OpenTelemetry.Api` to `1.3.1`.
([#665](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/665))

## 1.0.0-rc9.5 (source code moved to contrib repo)

Released 2022-Jun-21

* From this version onwards, the source code for this package would be hosted in
  the
  [contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule)
  repo. The source code for this package before this version was hosted on the
  [main](https://github.com/open-telemetry/opentelemetry-dotnet/tree/core-1.3.0/src/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule)
  repo.

## 1.0.0-rc9.4

Released 2022-Jun-03

## 1.0.0-rc9.3

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework version supported is
  .NET 4.6.2.
  ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08

* Adopted the donation
  [Microsoft.AspNet.TelemetryCorrelation](https://github.com/aspnet/Microsoft.AspNet.TelemetryCorrelation)
  from [.NET Foundation](https://dotnetfoundation.org/)
  ([#2223](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2223))

* Renamed the module, refactored existing code
  ([#2224](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2224)
  [#2225](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2225)
  [#2226](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2226)
  [#2229](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2229)
  [#2231](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2231)
  [#2235](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2235)
  [#2238](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2238)
  [#2240](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2240))

* Updated to use
  [ActivitySource](https://docs.microsoft.com/dotnet/api/system.diagnostics.activitysource)
  & OpenTelemetry.API
  ([#2249](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2249) &
  follow-ups (linked to #2249))

* TelemetryHttpModule will now restore Baggage on .NET 4.7.1+ runtimes when IIS
  switches threads
  ([#2314](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2314))

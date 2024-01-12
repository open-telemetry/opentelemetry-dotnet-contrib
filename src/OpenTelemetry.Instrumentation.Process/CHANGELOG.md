# Changelog

## Unreleased

## 0.5.0-beta.4

Released 2024-Jan-03

* Update `OpenTelemetry.Api` to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

## 0.5.0-beta.3

Released 2023-Jun-09

* Update OpenTelemetry API to 1.5.0
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

## 0.5.0-beta.2

Released 2023-Feb-27

* Update OpenTelemetry API to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

## 0.5.0-beta.1

Released 2023-Feb-17

> [!NOTE]
> The version number was lowered from 1.0.0 to 0.5.0 to better reflect the
experimental state of Opentelemetry process metrics specification status.
Packages that were older than this release will be delisted to avoid confusion.

* Added `process.cpu.count` metric.
  ([#981](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/981))

## 1.0.0-alpha.6

Released 2023-Feb-13

* Update OpenTelemetry API to 1.4.0-rc.4
  ([#990](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/990))

* Removed CPU utilization metric `process.cpu.utilization`.
  ([#972](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/972))

* Removed `ProcessInstrumentationOptions` and
  `AddProcessInstrumentation(this MeterProviderBuilder builder,`
  `Action<ProcessInstrumentationOptions>? configure)`
  from the public APIs.
  ([#973](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/973))

## 1.0.0-alpha.5

Released 2023-Feb-02

* Update OpenTelemetry API to 1.4.0-rc.3
  ([#944](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/944))

## 1.0.0-alpha.4

Released 2023-Jan-11

* Update OpenTelemetry API to 1.4.0-rc.2
  ([#880](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/880))

## 1.0.0-alpha.3

Released 2022-Dec-13

* Update OpenTelemetry API to 1.4.0-rc.1
  ([#820](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/820))

## 1.0.0-alpha.2

Released 2022-Nov-18

* Update OpenTelemetry API to 1.4.0-beta.3
  ([#774](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/774))

## 1.0.0-alpha.1

Released 2022-Nov-14

* Update the .NET API used to retrieve `process.memory.virtual` metric from
  [Process.PrivateMemorySize64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.privatememorysize64)
  to
  [Process.VirtualMemorySize64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.virtualmemorysize64).
  ([#762](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/762))

* Update OTel API version to be `1.4.0-beta.2` and change process metrics type
  from ObservableGauge to `ObservableUpDownCounter`. Updated instruments are:
  "process.memory.usage", "process.memory.virtual" and "process.threads".
  ([#751](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/751))

## 0.1.0-alpha.1

Released 2022-Oct-14

* This is the first release of `OpenTelemetry.Instrumentation.Process` package.

For more details, please refer to the [README](README.md).

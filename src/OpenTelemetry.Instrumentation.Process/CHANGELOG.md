# Changelog

## Unreleased

* Removed CPU utilization metric `process.cpu.utilization`.
  ([#972](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/972))

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

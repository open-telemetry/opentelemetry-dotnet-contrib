# Changelog

## 1.0.0-alpha.1

* Update the .NET API used to retrieve `process.memory.virtual` metric
  from [Process.PrivateMemorySize64]((https://learn.microsoft.com/dotnet/api/system.diagnostics.process.privatememorysize64)) to
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

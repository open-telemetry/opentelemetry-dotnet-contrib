# Changelog

## Unreleased

* Update OTel API version to be `1.4.0-beta.2` and change process metrics type
  from ObservableGauge to `ObservableUpDownCounter`.
  Instruments being affected are: "process.memory.usage",
  "process.memory.virtual" and "process.threads".
  ([#751](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/751))

## 0.1.0-alpha.1

Released 2022-Oct-14

* This is the first release of `OpenTelemetry.Instrumentation.Process` package.

For more details, please refer to the [README](README.md).

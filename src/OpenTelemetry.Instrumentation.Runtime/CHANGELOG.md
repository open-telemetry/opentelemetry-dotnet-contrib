# Changelog

## Unreleased

* Update OTel API version to `1.3.1`.
  ([#631](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/631))
* Update OTel API version to `1.4.0-beta.1` and change runtime metrics type to ObservableUpDownCounter
  ([#675](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/675))

## 1.0.0

Released 2022-Aug-03

* Rename `RuntimeInstrumentOptions` to `RuntimeInstrumentationOptions`
  ([#556](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/556))

## 1.0.0-rc.3

Released 2022-Jul-25

* Add gc.heap.fragmentation.size back for .NET 7 and later
  ([#524](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/524))

## 1.0.0-rc.2

Released 2022-Jul-19

* Refined some metrics names (assembly.count->assemblies.count,
  exception.count-> exceptions.count, attribute name: gen->generation) and
  descriptions
  ([#475](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/475))
* Change API for GC Heap Size for .NET 6 where the API has a bug
  ([#495](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/495))
* Remove gc.heap.fragmentation.size metrics due to buggy API on .NET 6
  ([#509](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/509))

## 1.0.0-rc.1

Released 2022-Jun-29

Major refactor of the runtime instrumentation. Renamed API signature and
metrics. Removed the options to turn off certain metrics.

## 1.0.0-beta.1

Major redesign of the runtime instrumentation. Renamed metrics to be more
user-friendly and better logical grouping. Removed the process related metrics
which are not .NET Runtime specific.

## 0.2.0-alpha.1

* Updated OTel SDK package version to 1.3.0
  ([#411](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/411))
* Fix some bugs in Runtime metrics
  ([#409](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/409))
* Add GC heap size and refactor GC count as multi-dimensional metrics in Runtime
  metrics
  ([#412](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/412))

## 0.1.0-alpha.1

* This is the first release of `OpenTelemetry.Instrumentation.Runtime` package.

For more details, please refer to the [README](README.md).

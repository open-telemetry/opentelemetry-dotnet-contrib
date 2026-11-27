# Changelog

## Unreleased

* Updated Semantic Conventions version to `1.45.0`.
  ([#6071](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/6071))

* Fixed the span name that samplers see. Spans now start with the
  `{rpc.method}` name. A span for a message that is not a method call starts
  with the `dotnet.remoting` name and the `rpc.system.name` attribute.
  ([#6104](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/6104))

* Fixed spans that omit `server.port` when the URI uses the default port of its
  scheme (`80` for `http` and `443` for `https`).
  ([#6104](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/6104))

* Fixed an outer span that stops early when a sampler drops or a filter
  rejects a nested synchronous call.
  ([#6104](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/6104))

## 0.1.0-alpha.8

Released 2026-Nov-11

* **Breaking Change** Dropped support for `net8.0` and `net9.0`.
  ([#3867](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867))

* Add support for .NET 11.0.
  ([#3867](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867))

* Updated OpenTelemetry core component version(s) to `1.21.0`.
  ([#5969](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5969))

## 0.1.0-alpha.7

Released 2026-Oct-21

* Updated OpenTelemetry core component version(s) to `1.20.1`.
  ([#5651](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5651))

## 0.1.0-alpha.6

Released 2026-Oct-15

* Updated OpenTelemetry core component version(s) to `1.20.0`.
  ([#5541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5541))

## 0.1.0-alpha.5

Released 2026-Sep-18

* Updated OpenTelemetry core component version(s) to `1.19.0`.
  ([#5240](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5240))

## 0.1.0-alpha.4

Released 2026-Aug-21

* Updated OpenTelemetry core component version(s) to `1.18.0`.
  ([#5022](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5022))

## 0.1.0-alpha.3

Released 2026-Jul-17

* Updated OpenTelemetry core component version(s) to `1.17.0`.
  ([#4773](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4773))

## 0.1.0-alpha.2

Released 2026-Jul-09

* Updated OpenTelemetry core component version(s) to `1.16.0`.
  ([#4487](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4487))

* Assemblies are now digitally signed using cosign.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4637))

## 0.1.0-alpha.1

Released 2026-Jun-01

* This is the first release of `OpenTelemetry.Instrumentation.Remoting` package.

For more details, please refer to the [README](README.md).

* Updated OpenTelemetry core component version(s) to `1.15.3`.
  ([#4166](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4166))

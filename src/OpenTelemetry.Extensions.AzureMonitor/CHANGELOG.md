# Changelog

## Unreleased

* Updated `ApplicationInsightsSampler` constructor to accept
  `ApplicationInsightsSamplerOptions` instead of a float `samplingRatio`, and
  introduced `ApplicationInsightsSamplerOptions` for Dependency Injection
  support.

## 1.0.0-beta.3

Released 2023-Feb-07

* Sampler will now return `RecordOnly` SamplingResult when the telemetry is
sampled instead of `Drop`. This will result in `Activity` to be created always
and populated with all information such as tag, events etc. This is done in
order to allow metrics collection from the generated activities.

> **Note**
> This change will have no impact on the overall sampling behavior,
but may have additional performance overhead from creating more activities.
([#933](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/933))

## 1.0.0-beta.2

Released 2022-Sept-12

* Replaced  TargetFrameworks from `net461` and `net6.0` to `netstandard2.0` and
  `net462`.
  ([#633](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/633))
* Changed `sampleRate` attribute type to `float`.
  ([#633](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/633))

## 1.0.0-beta.1

Released 2022-Sept-12

* Add "sampleRate" to `SamplingResult`.
  ([#623](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/623))

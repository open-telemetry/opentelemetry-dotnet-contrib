# Changelog - OpenTelemetry.Samplers.AWS

## Unreleased

* Update `System.Text.Json` to `8.0.4` to mitigate
  [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w).
  ([#2149](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2149))

## 0.1.0-alpha.2

Released 2024-Sep-09

* Performance problem fix for calling event source when required.
  ([#2046](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2046))

## 0.1.0-alpha.1

Released 2024-Jun-20

Initial release of `OpenTelemetry.Sampler.AWS`.

* Feature - AWSXRayRemoteSampler - Add support for AWS X-Ray remote sampling
  ([#1091](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1091),
   [#1124](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1124))

* Make OpenTelemetry.Sampler.AWS native AoT compatible.
  ([#1541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1541))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

# Changelog - OpenTelemetry.Samplers.AWS

## Unreleased

* Bumped the minimum required version of `System.Text.Json` to `8.0.4`
  in response to [CVE-2024-30105](https://github.com/dotnet/runtime/issues/104619).
  ([#1945](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1945))

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

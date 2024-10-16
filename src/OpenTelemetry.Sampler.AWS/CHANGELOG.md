# Changelog - OpenTelemetry.Samplers.AWS

## Unreleased

* Drop support for .NET 6 as this target is no longer supported and add .NET 8 target.
  ([#2172](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2172))

* Bumped the `System.Text.Json` reference to `6.0.10` for runtimes older than
  `net8.0` and added a direct reference for `System.Text.Json` at `8.0.5` on
  `net8.0` in response to
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#2197](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2197))

* Removed the `System.Net.Http` package reference from all targets.
  ([#2197](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2197))

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

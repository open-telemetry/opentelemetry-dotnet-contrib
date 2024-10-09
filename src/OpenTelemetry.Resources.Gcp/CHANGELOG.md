# Changelog

## Unreleased

* Add Google Cloud Platform resource detector for GKE, GAE, GCR, and GCE. Detector
  is accessible via `AddGcpDetector` extension method on `ResourceBuilder`.
  ([#1691](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1691))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

* Drop support for .NET 6 as this target is no longer supported and add .NET 8 target.
  ([#2167](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2167))

* Added direct reference to `System.Text.Json` for the `net8.0` target with
  minimum version of `8.0.5` in response to
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#2198](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2198))

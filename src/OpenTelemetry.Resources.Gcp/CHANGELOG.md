# Changelog

## Unreleased

* The minimum supported version of `Google.Api.Gax` is now `4.14.0`.
  ([#5212](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5212))

* Fixed a `NullReferenceException` thrown by the resource detector, which
  caused provider construction to fail when running as a Google Cloud Run job
  with `Google.Api.Gax` 4.14.0 or later. Cloud Run jobs are now detected
  (`cloud.platform` is `gcp_cloud_run`) with the `faas.name`,
  `gcp.cloud_run.job.execution` and `gcp.cloud_run.job.task_index` attributes.
  ([#5212](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5212))

* The detector now catches and logs an exception and returns an empty resource.
  ([#5212](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5212))

## 1.0.0-alpha.5

Released 2026-Sep-18

* Add `host.type` and `host.image.name` attributes to the Google Compute Engine
  resource detector.
  ([#5194](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5194))

* Updated semantic conventions to
  [v1.44.0](https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/resource/host.md).
  ([#5194](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5194))

* Updated OpenTelemetry core component version(s) to `1.19.0`.
  ([#5240](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5240))

## 1.0.0-alpha.4

Released 2026-Aug-21

* Updated OpenTelemetry core component version(s) to `1.18.0`.
  ([#5022](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5022))

## 1.0.0-alpha.3

Released 2026-Jul-22

* Updated OpenTelemetry core component version(s) to `1.17.0`.
  ([#4773](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4773))

* Add schema URL to resource detector.
  ([#4775](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4775))

* Fix `cloud.zone` being emitted instead of `cloud.availability_zone`.
  ([#4775](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4775))

## 1.0.0-alpha.2

Released 2026-Jul-09

* Update `System.Text.Json` for `netstandard2.0` to `8.0.5`.
  ([#4154](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4154))

* Updated OpenTelemetry core component version(s) to `1.16.0`.
  ([#4487](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4487))

* Assemblies are now digitally signed using cosign.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4637))

## 1.0.0-alpha.1

Released 2026-Apr-21

* Add Google Cloud Platform resource detector for GKE, GAE, GCR, and GCE. Detector
  is accessible via `AddGcpDetector` extension method on `ResourceBuilder`.
  ([#1691](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1691))

* Drop support for .NET 6 as this target is no longer supported and add .NET 8 target.
  ([#2167](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2167))

* Added direct reference to `System.Text.Json` for the `net8.0` target with
  minimum version of `8.0.5` in response to
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#2198](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2198))

* Add support for FaaS resource attributes for Google Cloud Run.
  ([#2789](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2789))

* Add support for .NET 10.0.
  ([#2822](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2822))

* Update .NET 10.0 NuGet package versions from `10.0.0-rc.2.25502.107` to `10.0.0`.
  ([#3403](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3403))

* Updated OpenTelemetry core component version(s) to `1.15.3`.
  ([#4166](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4166))

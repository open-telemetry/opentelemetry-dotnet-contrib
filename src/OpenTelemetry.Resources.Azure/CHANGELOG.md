# Changelog

## Unreleased

## 1.0.0-beta.8

Released 2024-Jun-18

* **Breaking Change**: Renamed method from `AddAppServiceDetector`
  to `AddAzureAppServiceDetector`.
  ([#1883](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1883))

* Updated OpenTelemetry core component version(s) to `1.9.0`.
  ([#1888](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1888))

## 1.0.0-beta.7

Released 2024-Jun-04

* **Breaking Change**: Renamed package from `OpenTelemetry.ResourceDetectors.Azure`
  to `OpenTelemetry.Resources.Azure`.
  ([#1840](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1840))

* **Breaking Change**: `AppServiceResourceDetector` type is now internal, use `ResourceBuilder`
  extension method `AddAppServiceDetector` to enable the detector.
  ([#1840](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1840))

* **Breaking Change**: `AzureVMResourceDetector` type is now internal, use `ResourceBuilder`
  extension method `AddAzureVMResourceDetector` to enable the detector.
  ([#1840](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1840))

* **Breaking Change**: `AzureContainerAppsResourceDetector` type is now
  internal, use `ResourceBuilder` extension method `AddAzureContainerAppsResourceDetector`
  to enable the detector.
  ([#1840](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1840))

* Update OpenTelemetry SDK version to `1.8.1`.
  ([#1668](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1668))

## 1.0.0-beta.6

Released 2024-Apr-05

* Update OpenTelemetry SDK version to `1.8.0`.
  ([#1635](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1635))

## 1.0.0-beta.5

Released 2024-Feb-05

* Added Azure Container Apps Resource Detector to generate attributes:
  `service.name`, `service.version`, `service.instance.id`, `cloud.provider` and
  `cloud.platform`.
  ([#1565](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1565))

## 1.0.0-beta.4

Released 2024-Jan-03

* Added NET6 target framework to support Trimming.
  ([#1405](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1405))

* Update OpenTelemetry SDK version to `1.7.0`.
  ([#1486](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1486))

## 1.0.0-beta.3

Released 2023-Sep-19

* Configured the `HttpClient` used for making the call to metadata service to
  use a `Timeout` to `2` seconds. This is to improve the start-up time of
  applications not running on Azure VMs.
  ([#1358](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1358))

* Updates to 1.6.0 of OpenTelemetry SDK.
  ([#1344](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1344))

* Suppress instrumentation for outgoing http call made to metadata service
  during `Detect()`.
  ([#1297](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1297))

## 1.0.0-beta.2

Released 2023-Jul-26

* Downgraded minimum required version for `System.Text.Json` to `4.7.2`.
  ([#1279](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1279))

## 1.0.0-beta.1

Released 2023-Jul-24

* For Azure VM Resource Detector:
  ([#1272](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1272/files))
  * **Updated attributes**: `azInst_vmId` to `host.id`, `azInst_location` to
    `cloud.region`, `azInst_name` to `host.name`, `azInst_osType` to `os.type`,
    `azInst_resourceId` to `cloud.resource_id`, `azInst_sku` to `azure.vm.sku`,
    `azInst_version` to `os.version`, `azInst_vmSize` to `host.type`,
    `azInst_vmScaleSetName` to `azure.vm.scaleset.name`.
  * **Added attributes**: `cloud.provider` and `cloud.platform`.
  * **Removed attributes**: `azInst_resourceGroupName`, `azInst_subscriptionId`.

* For Azure App Service:
 ([#1272](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1272/files))
  * **Updated attributes**: `appSrv_wsHost` to `host.id`, `appSrv_SlotName` to
    `deployment.environment`, `appSrv_wsStamp` to `azure.app.service.stamp`.
  * **Added attributes**: `cloud.resource_id`, `cloud.provider`,
    `cloud.platform`, `cloud.region`.
  * **Removed attribute**: `appSrv_ResourceGroup`.

* Updates to 1.5.0 of OpenTelemetry SDK.
  ([#1220](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1220))

* Added Azure VM resource detector.
  ([#1182](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1182))

## 1.0.0-alpha.1

Released 2023-Apr-19

* Add AppService resource detector.
  ([#989](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/989))

For more details, please refer to the [README](README.md).

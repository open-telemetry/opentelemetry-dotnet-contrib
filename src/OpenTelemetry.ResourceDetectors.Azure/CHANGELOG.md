# Changelog

## Unreleased

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

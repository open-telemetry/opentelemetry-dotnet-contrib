# Changelog

## Unreleased

## 1.7.0-alpha.1

Released 2023-Sep-22

* Allow overriding the Part B "name" field value in GenevaLogExporter.
  ([#1367](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1367))

## 1.6.0

Released 2023-Sep-09

* Update OpenTelemetry SDK version to `1.6.0`.
  ([#1346](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1346))

## 1.6.0-rc.1

Released 2023-Aug-28

* Update OpenTelemetry SDK version to `1.6.0-rc.1`.
  ([#1329](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1329))

## 1.6.0-alpha.1

Released 2023-Jul-12

* Update OpenTelemetry SDK version to `1.6.0-alpha.1`.
  ([#1264](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1264))

* Add back support for exporting `Exemplar`.
  ([#1264](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1264))

## 1.5.1

Released 2023-Jun-29

* Update OpenTelemetry SDK version to `1.5.1`.
  ([#1255](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1255))

## 1.5.0

Released 2023-Jun-14

* **Important Note:** Starting `1.5.0` version, `GenevaExporter` uses a newer
  format for exporting metrics. Please use `>= v2.2.2023.316.006` version of the
  MetricsExtension if you are using the metric exporter.

* Update OpenTelemetry SDK version to `1.5.0`.
  ([#1238](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1238))

* Removed support for exporting `Exemplars`. This would be added back in the
  `1.6.*` prerelease versions right after `1.5.0` stable version is released.
  ([#1238](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1238))

* Add named options support for `GenevaTraceExporter` and
  `GenevaMetricExporter`.
  ([#1218](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1218))

* Add a new overload for `AddGenevaMetricExporter` without any parameters to
  avoid warning
  [RS0026](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/Microsoft.CodeAnalysis.PublicApiAnalyzers.md#rs0026-do-not-add-multiple-public-overloads-with-optional-parameters).
  ([#1218](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1218))

* Fix the issue of running into the `ArgumentException`: `An instance of
  EventSource with Guid edc24920-e004-40f6-a8e1-0e6e48f39d84 already exists.`
  when using multiple instances of `GenevaMetricExporter`.
  ([#1225](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1225))

## 1.5.0-rc.1

Released 2023-Jun-05

* Fix an issue with getting sanitized category name in pass-through table name
  mapping cases for `TldLogExporter`.
  ([#1175](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1175))

* TldLogExporter to export `SpanId` value in `ext_dt_spanId` field instead of
  `TraceId` value.
  ([#1184](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1184))

* Add support for abstract domain sockets.
  ([#1199](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1199))

* Update OpenTelemetry SDK version to `1.5.0-rc.1`.
  ([#1210](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1210))

## 1.5.0-alpha.3

Released 2023-Apr-19

* TldLogExporter to export `eventId.Id` as a Part B field instead of Part C
  field.
  ([#1134](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1134))

* Update GenevaLogExporter to export `eventId.Name` as the value for Part A
  `name` field when the `EventNameExportMode` option is set to
  `ExportAsPartAName`.
  ([#1135](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1135))

## 1.4.1

Released 2023-Mar-29

* Relaxed table name mapping validation rules to restore the previous behavior
  from version 1.3.0.
  ([#1120](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1120))

## 1.5.0-alpha.2

Released 2023-Mar-29

* Fix a bug where metrics without exemplars were not getting exported.
  ([#1099](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1099))

* Relaxed table name mapping validation rules to restore the previous behavior
  from version 1.3.0.
  ([#1109](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1109))

* Add support for exporting metrics to more than a single account/namespace
  combination using a single GenevaMetricExporter instance. Users can now export
  individual metric streams to:
  * An account of their choice by adding the dimension
    `_microsoft_metrics_account` and providing a `string` value for it as the
    account name.
  * A metric namespace of their choice by adding the dimension
    `_microsoft_metrics_namespace` and providing a `string` value for it as the
    namespace name.
  ([#1111](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1111))

* Fix a bug in TldTraceExporter for incorrect serialization of special tags.
  ([#1115](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1115))

## 1.5.0-alpha.1

Released 2023-Mar-13

* Changed the behavior of Unix domain socket connection at startup. Before this
  change, the exporter initialization would throw exception if the target Unix
  Domain Socket does not exist. After this change, the exporter initialization
  would return success and the exporting background thread will try to establish
  the connection.
  ([#935](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/935))

* Update OpenTelemetry SDK version to `1.5.0-alpha.1`.
* Update GenevaMetricExporter to use TLV format serialization.
* Add support for exporting exemplars.
  ([#1069](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1069))

## 1.4.0

Released 2023-Feb-27

* Update OpenTelemetry to 1.4.0
  ([#1038](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1038))

* Add `DisableMetricNameValidation` connection string flag for controlling
  metric name validation performed by the OpenTelemetry SDK.
  ([#1006](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/1006))

## 1.4.0-rc.4

Released 2023-Feb-13

* Update OpenTelemetry to 1.4.0-rc.4
  ([#990](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/990))

## 1.4.0-rc.3

Released 2023-Feb-08

* Update OpenTelemetry to 1.4.0-rc.3
  ([#944](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/944))

## 1.4.0-rc.2

Released 2023-Jan-30

* Update OpenTelemetry to 1.4.0-rc.2
  ([#880](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/880))

* Add TldTraceExporter and TldLogExporter. Use
  `"PrivatePreviewEnableTraceLoggingDynamic=true"` in the connection string to
  use these exporters.
  ([#662](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/662))
  ([#874](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/874))

* Add support for configuring BatchActivityExportProcessor parameters (via
  environment variables) used by GenevaTraceExporter in Linux.
  ([#925](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/925))

## 1.4.0-rc.1

Released 2022-Dec-19

* Update OpenTelemetry to 1.4.0-rc.1
  ([#820](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/820))
* Add support in logs for prefix-based table name mapping configuration.
  [#796](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/796)
* Updated the trace exporter to use the new performance APIs introduced in
  `System.Diagnostics.DiagnosticSource` v7.0.
  [#838](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/838)
* Avoid allocation when serializing scopes.
  ([#818](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/818))

## 1.4.0-beta.6

Released 2022-Dec-09

* Added support for
  [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset).
  ([#797](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/797))
* Fix the overflow bucket value serialization for Histogram.
  ([#805](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/805))
* Fix EventSource logging.
  ([#813](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/813))
* Update `MessagePackSerializer` to use
  [BinaryPrimitives](https://learn.microsoft.com/dotnet/api/system.buffers.binary.binaryprimitives)
  to serialize scalar types more efficiently by avoiding repeated bound checks.
  Add support for serializing `ISpanFormattable` types.
  ([#803](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/803))

## 1.3.1

Released 2022-Dec-07

* Fix the overflow bucket value serialization for Histogram.
  ([#807](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/807))

## 1.4.0-beta.5

Released 2022-Nov-21

* Update OpenTelemetry to 1.4.0-beta.3
  ([#774](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/774))

## 1.4.0-beta.4

Released 2022-Oct-28

* Updated export logic for scopes
  * Users upgrading from `1.4.0-beta.1`, `1.4.0-beta.2` or `1.4.0-beta.3` to
    this version will see a **breaking change**
  * Export scopes which have a non-null key as individual columns (each
    key-value pair from the scopes is exported as its own column; these columns
    would also be taken into consideration when the CustomFields option is
    applied).
  * When using formatted strings for scopes, the templated string
    (`"{OriginalFormat"}`) will not be exported.
  [#736](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/736)

## 1.4.0-beta.3

Released 2022-Oct-20

* Add support for exporting `UpDownCounter` and `ObservableUpDownCounter`.
  [#685](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/685)

* Export `MetricType.LongGauge` as a double metric as it might return negative
  values.
  [#721](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/721)

* Add support for exporting exception stack.
  [#672](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/672)

* Change the default MetricExportInterval from 20 seconds to 60 seconds.
  [#722](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/722)

## 1.4.0-beta.2

Released 2022-Oct-17

* The option `TableNameMappings` of `GenevaExporterOptions` will not support
  string values that are null, empty, or consist only of white-space characters.
  It will also not support string values that contain non-ASCII characters.
  [646](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/646)

* Update OpenTelemetry SDK version to `1.4.0-beta.2`. Add support for exporting
  Histogram Min and Max. If the histogram does not contain min and max,
  the exporter exports both the values as zero.
  [#704](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/704)

## 1.4.0-beta.1

Released 2022-Aug-01

## 1.3.1-alpha.0.5 (unlisted due to incorrect version)

Released 2022-Jul-29

* Add support for exporting `ILogger` scopes.
[545](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/545)

## 1.3.0

Released 2022-Jul-28

* Supports `OpenTelemetry.Extensions.Hosting` based configuration for
`GenevaMetricExporter`.
[397](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/397)

* Update OpenTelemetry SDK version to `1.3.0`.
[427](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/427)

* Remove support for .NET Framework 4.6.1. The minimum .NET Framework version
supported now is .NET 4.6.2.
[441](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/441)

* Fix the incorrect `ExportResult` issue on Linux:
[422](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/422)
by throwing any exception caught by `UnixDomainSocketDataTransport.Send` so that
`Export` methods cn catch it and correctly set `ExportResult` to
`ExportResult.Failure`.
[444](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/444)

* The option `PrepopulatedFields` of `GenevaExporterOptions` will only support
values of type: `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`, `float`, `double`, and `string`. It will also not accept `null`
values.
[514](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/514)
[537](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/537)

* The option `MetricExportIntervalMilliseconds` of `GenevaMetricExporterOptions`
will not accept a value less than 1000.
[527](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/527)

* Remove support for exporting `ILogger` scopes that was added in `1.3.0-beta.2`
version.
[541](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/541)

## 1.3.0-beta.2

Released 2022-Jun-03

* Add support for exporting `ILogger` scopes.
[390](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/390)

## 1.3.0-beta.1

Released 2022-May-27

* Enable PassThru TableNameMappings using the logger category name.
[345](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/345)

* Throw exception when `TableNameMappings` contains a `null` value.
[322](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/322)

* TraceExporter bug fix to not export non-recorded Activities.
[352](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/352)

* Add support for the native `Activity` properties `Status` and
`StatusDescription`.
[359](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/359)

* Allow serialization of non-ASCII characters for
`LogRecord.Exception.GetType().FullName`.
[375](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/375)

## 1.2.6

Released 2022-Apr-21

* Set GenevaMetricExporter temporality preference back to Delta.
[323](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/323)

## 1.2.5 Broken

Released 2022-Apr-20

Note: This release was broken due to the GenevaMetricExporter using a
TemporalityPreference of Cumulative instead of Delta, it has been unlisted from
NuGet.
[303](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/303)
is the PR that introduced this bug to GenevaMetricExporterExtensions.cs

* Update OpenTelemetry SDK version to `1.2.0`.
[319](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/319)

## 1.2.4 Broken

Released 2022-Apr-20

This is the first release of the `OpenTelemetry.Exporter.Geneva` project. Note:
This release was broken due to using OpenTelemetry 1.2.0-rc5. Therefore, it has
been unlisted on NuGet.

* LogExporter modified to stop calling `ToString()` on `LogRecord.State` to
obtain Log body. It now obtains body from `LogRecord.FormattedMessage` or
special casing "{OriginalFormat}" only.
[295](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/295)

* Fixed a bug which causes LogExporter to not serialize if the `LogRecord.State`
had a single KeyValuePair.
[295](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/295)

* Update OpenTelemetry SDK version to `1.2.0-rc5`.
[308](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/308)

# Changelog

## Unreleased

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

* Update OTel SDK version to `1.3.0`.
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

* Update OTel SDK version to `1.2.0`.
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

* Update OTel SDK version to `1.2.0-rc5`.
[308](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/308)

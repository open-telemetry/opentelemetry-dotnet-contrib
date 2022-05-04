# Geneva Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Geneva.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Geneva)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Geneva.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Geneva)

The Geneva Exporter exports telemetry to
[Event Tracing for Windows (ETW)](https://docs.microsoft.com/windows/win32/etw/about-event-tracing)
or to a
[Unix Domain Socket (UDS)](https://en.wikipedia.org/wiki/Unix_domain_socket)
on the local machine.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Geneva
```

## Configuration

The three types of telemetry are handled separately in OpenTelemetry.
Therefore, each type of telemetry **must be** enabled separately.

### Enable Logs

Install the latest stable version of
[`Microsoft.Extensions.Logging`](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)

```shell
dotnet add package OpenTelemetry.Exporter.Geneva
```

This snippet shows how to configure the Geneva Exporter for logs

```csharp
using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
    .AddOpenTelemetry(openTelemetryLoggerOptions =>
    {
        openTelemetryLoggerOptions.AddGenevaLogExporter(genevaExporterOptions =>
        {
            genevaExporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
        });
    }));
```

The above code must be in application startup. In case of ASP.NET Core
applications, this should be in `ConfigureServices` of `Startup` class.
For ASP.NET applications, this should be in `Global.aspx.cs`.

Since OpenTelemetry .NET SDK is a
[LoggingProvider](https://docs.microsoft.com/dotnet/core/extensions/logging-providers),
use the built-in mechanism to apply [log
filtering](https://docs.microsoft.com/dotnet/core/extensions/logging?tabs=command-line#how-filtering-rules-are-applied).
This filtering lets you control the logs that are sent to each registered
provider, including the OpenTelemetry provider. `OpenTelemetry` is the
[alias](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.provideraliasattribute)
for `OpenTelemetryLoggerProvider`, that may be used when configuring filtering
rules.

**NOTE:** _Some application types (e.g. [ASP.NET
Core](https://docs.microsoft.com/aspnet/core/fundamentals/logging/#configure-logging-1))
have default logging settings. Please review them to make sure
`OpenTelemetryLoggingProvider` is configured to receive logs of appropriate
levels and category.

### Enable Traces

This snippet shows how to configure the Geneva Exporter for traces

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new AlwaysOnSampler())
    .AddSource("DemoSource")
    .AddGenevaTraceExporter(options => {
        options.ConnectionString = "EtwSession=OpenTelemetry";
    })
    .Build();
```

The above code must be in application startup. In case of ASP.NET Core
applications, this should be in `ConfigureServices` of `Startup` class.
For ASP.NET applications, this should be in `Global.aspx.cs`.

### GenevaExporterOptions (for logs and traces)

`GenevaExporterOptions` contains various options to configure the Geneva
Exporter.

#### `ConnectionString` (required)

On Linux the connection string has the format `Endpoint=unix:{UDS Path}`.

On Windows the connection string has the format `EtwSession={ETW session}`.

#### `CustomFields` (optional)

A list of fields which should be stored as individual table columns.

#### `PrepopulatedFields` (optional)

This is a collection of fields that will be applied to all the logs and traces
sent through this exporter.

#### `TableNameMappings` (optional)

This defines the mapping for the table name used to store traces and logs.

The default table name used for traces is `Span`. For changing the table name
for traces, add an entry with key as `Span`, and value as the custom table name.

The default table name used for logs is `Log`. Mappings can be specified for
each
[category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category)
of the log. For changing the default table name for logs, add an entry with key
as `*`, and value as the custom table name.

### Enable Metrics

**NOTE:** _Version 1.2.x of the Geneva Exporter (which adds Metrics support)
does not support .NET versions lower than .NET Framework 4.6.1._

This snippet shows how to configure the Geneva Exporter for metrics

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("TestMeter")
    .AddGenevaMetricExporter(options =>
    {
        options.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
    })
    .Build();
```

The above code must be in application startup. In case of ASP.NET Core
applications, this should be in `ConfigureServices` of `Startup` class.
For ASP.NET applications, this should be in `Global.aspx.cs`.

### GenevaMetricExporterOptions (for metrics)

`GenevaMetricExporterOptions` contains various options which are required to
configure the GenevaMetricExporter.

#### `ConnectionString` (required for metrics)

On Windows **DO NOT** provide an ETW session name for Metrics, only specify Account and
Namespace. For example:
`Account={MetricAccount};Namespace={MetricNamespace}`.

On Linux provide an `Endpoint` in addition to the `Account` and `Namespace`.
For example:
`Endpoint=unix:{UDS Path};Account={MetricAccount};Namespace={MetricNamespace}`.

#### `MetricExportIntervalMilliseconds` (optional)

Set the exporter's periodic time interval to export metrics. The default value
is 20000 milliseconds.

#### `PrepopulatedMetricDimensions` (optional)

This is a collection of the dimensions that will be applied to _every_ metric
exported by the exporter.

## Troubleshooting

Before digging into a problem, check if you hit a known issue by looking at the
[CHANGELOG.md](./CHANGELOG.md) and [GitHub
issues](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues).

Geneva Exporters uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Exporter-Geneva" for its internal logging. Please
follow the [troubleshooting guide for OpenTelemetry
.NET](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry#troubleshooting)
for instructions on seeing logs from the geneva exporter, as well as other
OpenTelemetry components.

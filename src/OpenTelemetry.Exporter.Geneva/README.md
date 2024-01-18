# Geneva Exporter for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Geneva)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Geneva)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Geneva)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Geneva)

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

This snippet shows how to configure the Geneva Exporter for Logs

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
use the built-in mechanism to apply [Log
filtering](https://docs.microsoft.com/dotnet/core/extensions/logging?tabs=command-line#how-filtering-rules-are-applied).
This filtering lets you control the Logs that are sent to each registered
provider, including the OpenTelemetry provider. `OpenTelemetry` is the
[alias](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.provideraliasattribute)
for `OpenTelemetryLoggerProvider`, that may be used when configuring filtering
rules.

> [!NOTE]
> Some application types (e.g. [ASP.NET
Core](https://docs.microsoft.com/aspnet/core/fundamentals/logging/#configure-logging-1))
have default logging settings. Please review them to make sure
`OpenTelemetryLoggingProvider` is configured to receive Logs of appropriate
levels and category.

### Enable Traces

This snippet shows how to configure the Geneva Exporter for Traces

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

### GenevaExporterOptions (for Logs and Traces)

`GenevaExporterOptions` contains various options to configure the Geneva
Exporter.

#### `ConnectionString` (required for Logs and Traces)

On Linux the connection string has the format `Endpoint=unix:{UDS Path}`.

On Windows the connection string has the format `EtwSession={ETW session}`.

#### `CustomFields` (optional)

A list of fields which should be stored as individual table columns.

* If null, all fields will be stored as individual columns.
* If non-null, only those fields named in the list will be stored as individual columns.

#### `PrepopulatedFields` (optional)

This is a collection of fields that will be applied to all the Logs and Traces
sent through this exporter.

#### `TableNameMappings` (optional)

This defines the mapping for the table name used to store Logs and Traces.

##### Trace table name mappings

The default table name used for Traces is `Span`. To change the table name for
Traces add an entry with the key `Span` and set the value to the desired custom
table name.

> [!NOTE]
> Only a single table name is supported for Traces.

##### Log table name mappings

The default table name used for Logs is `Log`. Mappings can be specified
universally or for individual log message
[category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category)
values.

* To change the default table name for Logs add an entry with the key `*` and
  set the value to the desired custom table name. To enable "pass-through"
  mapping set the value of the `*` key to `*`. For details on "pass-through"
  mode see below.

* To change the table name for a specific log
  [category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category)
  add an entry with the key set to the full "category" of the log messages or a
  prefix that will match the starting portion of the log message "category". Set
  the value of the key to either the desired custom table name or `*` to enable
  "pass-through" mapping. For details on "pass-through" mode see below.

  For example, given the configuration...

  ```csharp
    var options = new GenevaExporterOptions
    {
        TableNameMappings = new Dictionary<string, string>()
        {
            ["*"] = "DefaultLogs",
            ["MyCompany"] = "InternalLogs",
            ["MyCompany.Product1"] = "InternalProduct1Logs",
            ["MyCompany.Product2"] = "InternalProduct2Logs",
            ["MyCompany.Product2.Security"] = "InternalSecurityLogs",
            ["MyPartner"] = "*",
        },
    };
  ```

  ...log category mapping would be performed as such:

  * `ILogger<ThirdParty.Thing>`: This would go to "DefaultLogs"
  * `ILogger<MyCompany.ProductX.Thing>`: This would go to "InternalLogs"
  * `ILogger<MyCompany.Product1.Thing>`: This would go to "InternalProduct1Logs"
  * `ILogger<MyCompany.Product2.Thing>`: This would go to "InternalProduct2Logs"
  * `ILogger<MyCompany.Product2.Security.Alert>`: This would go to
    "InternalSecurityLogs"
  * `ILogger<MyPartner.Product.Thing>`: This is marked as pass-through ("*") so
    it will be sanitized as "MyPartnerProductThing" table name

##### Pass-through table name mapping rules

When "pass-through" mapping is enabled for a given log message the runtime
[category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category)
value will be converted into a valid table name.

* The first character MUST be an ASCII letter. If it is lower-case, it will be
  converted into an upper-case letter. If the first character is invalid all log
  messages for the "category" will be dropped.

* Any non-ASCII letter or number will be removed.

* Only the first 50 valid characters will be used.

### Enable Metrics

This snippet shows how to configure the Geneva Exporter for Metrics

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

### GenevaMetricExporterOptions (for Metrics)

`GenevaMetricExporterOptions` contains various options which are required to
configure the GenevaMetricExporter.

#### `ConnectionString` (required for Metrics)

On Windows **DO NOT** provide an ETW session name for Metrics, only specify
Account and Namespace. For example:
`Account={MetricAccount};Namespace={MetricNamespace}`.

On Linux provide an `Endpoint` in addition to the `Account` and `Namespace`.
For example:
`Endpoint=unix:{UDS Path};Account={MetricAccount};Namespace={MetricNamespace}`.

#### `MetricExportIntervalMilliseconds` (optional)

Set the exporter's periodic time interval to export Metrics. The default value
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
for instructions on seeing Logs from the geneva exporter, as well as other
OpenTelemetry components.

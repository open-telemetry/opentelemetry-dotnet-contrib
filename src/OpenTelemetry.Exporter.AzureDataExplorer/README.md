# Azure Data Explorer Exporter for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@DamianEdwards](https://github.com/DamianEdwards) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.AzureDataExplorer)](https://www.nuget.org/packages/OpenTelemetry.Exporter.AzureDataExplorer)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.AzureDataExplorer)](https://www.nuget.org/packages/OpenTelemetry.Exporter.AzureDataExplorer)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Exporter.AzureDataExplorer)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Exporter.AzureDataExplorer)

This exporter sends traces, metrics and logs directly from a .NET process to
[Azure Data Explorer](https://learn.microsoft.com/azure/data-explorer/) (Kusto),
including Kusto databases in Microsoft Fabric Real-Time Intelligence.

It is an in-process equivalent of the OpenTelemetry Collector
[Azure Data Explorer exporter](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/azuredataexplorerexporter)
and writes rows using the same table schemas, so tables, mappings, update
policies and queries created for the Collector exporter can be reused.

Data is ingested by calling the Kusto and Azure Storage REST APIs directly with
`HttpClient`, without a dependency on the Kusto SDK, so the exporter is
compatible with trimming and native AOT.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.AzureDataExplorer
```

## Prerequisites

Create the database and tables. The default database is `oteldb` and the
default tables are `OTELTraces`, `OTELMetrics` and `OTELLogs`:

```kql
.create-merge table OTELLogs (Timestamp:datetime, ObservedTimestamp:datetime, TraceID:string, SpanID:string, SeverityText:string, SeverityNumber:int, Body:string, ResourceAttributes:dynamic, LogsAttributes:dynamic)

.create-merge table OTELMetrics (Timestamp:datetime, MetricName:string, MetricType:string, MetricUnit:string, MetricDescription:string, MetricValue:real, Host:string, ResourceAttributes:dynamic, MetricAttributes:dynamic, Exemplars:dynamic)

.create-merge table OTELTraces (TraceID:string, SpanID:string, ParentID:string, SpanName:string, SpanStatus:string, SpanStatusMessage:string, SpanKind:string, StartTime:datetime, EndTime:datetime, ResourceAttributes:dynamic, TraceAttributes:dynamic, Events:dynamic, Links:dynamic)
```

Grant the identity used by the exporter the
[Database Ingestor](https://learn.microsoft.com/azure/data-explorer/kusto/access-control/role-based-access-control)
role on the database.

When using `AzureDataExplorerIngestionType.Managed` ingestion,
[streaming ingestion](https://learn.microsoft.com/azure/data-explorer/ingest-data-streaming)
must be enabled on the cluster and the database or tables:

```kql
.alter table OTELLogs policy streamingingestion enable
```

Managed ingestion falls back to queued ingestion when streaming ingestion
fails transiently, the payload is too large, or the table cannot accept
streamed data (for example because of an incompatible update policy). When
Kusto reports that streaming ingestion is not enabled for the table or the
cluster, the exporter uses queued ingestion for that table for the next
15 minutes before trying streaming again.

## Usage

```csharp
using OpenTelemetry.Exporter.AzureDataExplorer;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

static void ConfigureAdx(AzureDataExplorerExporterOptions options)
{
    options.ClusterUri = new Uri("https://mycluster.westus.kusto.windows.net");
    options.ManagedIdentityId = "system";
}

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddAzureDataExplorerExporter(ConfigureAdx))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddAzureDataExplorerExporter(ConfigureAdx))
    .WithLogging(logging => logging
        .AddAzureDataExplorerExporter(ConfigureAdx));
```

Traces and logs are exported using a `BatchActivityExportProcessor` /
`BatchLogRecordExportProcessor` and metrics are exported using a
`PeriodicExportingMetricReader`. Batch and reader settings are configured
via `BatchExportActivityProcessorOptions`, `BatchExportLogRecordProcessorOptions`
and `MetricReaderOptions` (including `TemporalityPreference`), optionally
using the same name passed to `AddAzureDataExplorerExporter`.

Named options are supported, for example to send different signals to
different clusters:

```csharp
builder.Services.Configure<AzureDataExplorerExporterOptions>(
    "adx",
    builder.Configuration.GetSection("AzureDataExplorer"));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAzureDataExplorerExporter("adx", configure: null));
```

## Configuration

| Option | Collector setting | Default | Description |
| --- | --- | --- | --- |
| `ClusterUri` | `cluster_uri` | (required) | Cluster engine URI. The ingestion endpoint is derived automatically for queued ingestion. |
| `ApplicationId` | `application_id` | | Microsoft Entra application (client) ID. |
| `ApplicationKey` | `application_key` | | Microsoft Entra application key (secret). |
| `TenantId` | `tenant_id` | | Microsoft Entra tenant ID. |
| `ManagedIdentityId` | `managed_identity_id` | | `system` or the client ID (GUID) of a user-assigned managed identity. |
| `UseAzureAuth` | `use_azure_auth` | `false` | Authenticate using `DefaultAzureCredential`. |
| `TokenCredential` | (none) | | Any `Azure.Core.TokenCredential`. |
| `DatabaseName` | `db_name` | `oteldb` | Database name. |
| `MetricsTableName` | `metrics_table_name` | `OTELMetrics` | Metrics table. |
| `LogsTableName` | `logs_table_name` | `OTELLogs` | Logs table. |
| `TracesTableName` | `traces_table_name` | `OTELTraces` | Traces table. |
| `MetricsTableJsonMapping` | `metrics_table_json_mapping` | | Optional pre-created JSON ingestion mapping. |
| `LogsTableJsonMapping` | `logs_table_json_mapping` | | Optional pre-created JSON ingestion mapping. |
| `TracesTableJsonMapping` | `traces_table_json_mapping` | | Optional pre-created JSON ingestion mapping. |
| `IngestionType` | `ingestion_type` | `Queued` | `Queued` or `Managed` (streaming with queued fallback). |
| `ExportTimeout` | `timeout` | 30 seconds | Maximum time for one export, including retries. |
| `RetryOnFailure` | `retry_on_failure.enabled` | `true` | Retry transient failures with exponential backoff. |
| `RetryInitialInterval` | `retry_on_failure.initial_interval` | 5 seconds | Initial retry delay. |
| `RetryMaxInterval` | `retry_on_failure.max_interval` | 30 seconds | Maximum retry delay. |
| `HttpClientFactory` | (none) | Shared client | Factory for the `HttpClient` used for all requests. |

Exactly one authentication method must be configured: the
`ApplicationId`/`ApplicationKey`/`TenantId` triple, `ManagedIdentityId`,
`UseAzureAuth` or `TokenCredential`.

Like the Collector exporter, this exporter is configured with a cluster URI
and a credential. It does not accept Kusto connection strings (for example
`Data Source=...;Fed=true;...`), and it does not implement the Kusto SDK's own
authentication modes (such as application certificate, user prompt or
pre-acquired user/application tokens). For those cases, construct the
equivalent [Azure.Identity](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
credential, for example `ClientCertificateCredential` or
`InteractiveBrowserCredential`, and assign it to `TokenCredential`:

```csharp
options.ClusterUri = new Uri("https://mycluster.westus.kusto.windows.net");
options.TokenCredential = new ClientCertificateCredential(tenantId, clientId, certificate);
```

`HttpClientFactory` is called once for each exporter (one per signal), and the
exporter does not dispose the returned client. The default shared client
recycles pooled connections every 5 minutes so that long-running applications
pick up DNS changes; on .NET Framework this is done by setting
`ServicePoint.ConnectionLeaseTimeout` for the endpoints the exporter calls.
When you supply your own client, configure the equivalent behavior yourself
(for example `SocketsHttpHandler.PooledConnectionLifetime`).

Failures that Kusto reports as permanent (for example a missing table or an
invalid mapping) are not retried. Authorization failures (HTTP 401/403) on
streaming ingestion are permanent. In queued mode, failures to fetch ingestion
resources or the identity token are retried, since they can be transient.
When Kusto returns HTTP 401, the exporter discards its cached access token and
retries the request once if the credential issues a new token.

## Data mapping

The rows written match the Collector exporter:

* **Traces**: one row per span. `TraceAttributes` contains the span
  attributes plus `scope.name` and `scope.version`. `Events` and `Links` are
  arrays of `{EventName, Timestamp, EventAttributes}` and
  `{TraceID, SpanID, TraceState, SpanLinkAttributes}`. Status codes are written
  as `STATUS_CODE_UNSET|OK|ERROR` and kinds as `SPAN_KIND_*`.
* **Logs**: one row per log record. `LogsAttributes` contains the record
  attributes plus `scope.name` (the logger category) and `scope.version`.
* **Metrics**: one row per data point. Sums (counters and up-down counters)
  are written with type `Sum` and gauges with type `Gauge`. Histograms are
  expanded into `<name>_sum`, `<name>_count`, `<name>_min`, `<name>_max` and
  cumulative `<name>_bucket` rows with an `le` attribute (the last bucket is
  `+Inf`). `Host` is taken from the `host.name` resource attribute, falling
  back to the machine name. Exemplars are written to the `Exemplars` column.

## Differences from the Collector exporter

* Exponential histograms are not supported and are skipped (a warning is
  written to the EventSource). The .NET SDK does not produce `Summary`
  metrics.
* Metric points with `NaN` or infinite values are not exported.
* Timestamps have 100 nanosecond precision. `ObservedTimestamp` is the same as
  `Timestamp` for logs.
* Log `SeverityText`/`SeverityNumber` are derived from `LogLevel` (for example
  `Information` maps to `Info`/`9`).
* Log records also include `event.id`, `event.name` and `exception.*`
  attributes when present. The log `Body` is the formatted message when
  `OpenTelemetryLoggerOptions.IncludeFormattedMessage` is enabled, otherwise the
  message template.
* There is no persistent sending queue; the SDK batch processor and periodic
  reader buffer telemetry in memory. Data in these buffers is lost if the
  process terminates before it is exported.
* `TokenCredential` and `HttpClientFactory` are additional options.

## Troubleshooting

This component uses an
[EventSource](https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name `OpenTelemetry-Exporter-AzureDataExplorer` for its internal
logging. Please follow the
[troubleshooting guide](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/README.md#troubleshooting)
for how to collect these logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Azure Data Explorer](https://learn.microsoft.com/azure/data-explorer/)
* [OpenTelemetry Collector Azure Data Explorer exporter](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/azuredataexplorerexporter)

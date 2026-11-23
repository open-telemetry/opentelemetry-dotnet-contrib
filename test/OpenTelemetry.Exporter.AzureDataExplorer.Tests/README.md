# OpenTelemetry.Exporter.AzureDataExplorer.Tests

This project has three kinds of tests.

## Unit tests

These run everywhere and need no external services. They use in-memory
ingestors to cover serialization, options, retries and lifecycle behavior.

## Emulator integration tests

`AzureDataExplorerEmulatorTests` export traces, metrics and logs to the
[Kusto emulator](https://learn.microsoft.com/azure/data-explorer/kusto-emulator-overview)
container. They use the exporter's REST streaming ingestion and the
table definitions from the component
[README](../../src/OpenTelemetry.Exporter.AzureDataExplorer/README.md#prerequisites).
They need Docker running Linux containers and are skipped when it is not
available. CI runs them on Linux.

```sh
dotnet test test/OpenTelemetry.Exporter.AzureDataExplorer.Tests --filter "CategoryName=AzureDataExplorerEmulatorTests"
```

The emulator does not support Microsoft Entra authentication or queued
ingestion, so these tests connect without authentication and use managed
(streaming) ingestion only.

## Cluster integration tests

`AzureDataExplorerClusterTests` export all signals to a real Azure Data
Explorer cluster using both queued and managed ingestion. They are opt-in and
skipped unless `OTEL_ADX_TEST_CLUSTER_URI` is set.

Before running them:

1. Create the database and tables using the KQL in the component
   [README](../../src/OpenTelemetry.Exporter.AzureDataExplorer/README.md#prerequisites).
2. Enable streaming ingestion on the cluster and database
   (`.alter database <db> policy streamingingestion enable`) for managed
   ingestion.
3. Grant the test identity the `Ingestor` and `Viewer` database roles.

| Variable | Required | Description |
| --- | --- | --- |
| `OTEL_ADX_TEST_CLUSTER_URI` | Yes | Cluster URI, for example `https://mycluster.westus.kusto.windows.net`. |
| `OTEL_ADX_TEST_DATABASE` | No | Database name. Defaults to `oteldb`. |
| `OTEL_ADX_TEST_APPLICATION_ID` | No | Application (client) ID for service principal authentication. |
| `OTEL_ADX_TEST_APPLICATION_KEY` | No | Application key, used with `OTEL_ADX_TEST_APPLICATION_ID`. |
| `OTEL_ADX_TEST_TENANT_ID` | No | Tenant ID, used with `OTEL_ADX_TEST_APPLICATION_ID`. |
| `OTEL_ADX_TEST_MANAGED_IDENTITY_ID` | No | Managed identity client ID, or `system`. |
| `OTEL_ADX_TEST_TIMEOUT_MINUTES` | No | Time to wait for ingested rows. Defaults to `10`. |

If neither an application ID nor a managed identity is set, the tests use
`DefaultAzureCredential`, for example the account signed in with `az login`.

```sh
export OTEL_ADX_TEST_CLUSTER_URI=https://mycluster.westus.kusto.windows.net
dotnet test test/OpenTelemetry.Exporter.AzureDataExplorer.Tests --filter "CategoryName=AzureDataExplorerClusterTests"
```

Queued ingestion is batched by the service according to the table's
ingestion batching policy, so these tests can take several minutes. Each run
writes rows tagged with a unique `service.name` and does not delete them.
On a dedicated test database you can shorten the batching window to reduce
the wait:

```kql
.alter database <db> policy ingestionbatching '{"MaximumBatchingTimeSpan":"00:00:10","MaximumNumberOfItems":500,"MaximumRawDataSizeMB":1024}'
```

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Data;
using OpenTelemetry.Tests;
using Testcontainers.Kusto;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// Starts the Kusto emulator and creates the exporter tables using the KQL documented in the README.
/// </summary>
public sealed class KustoEmulatorFixture : ContainerFixture<KustoContainer>, IAsyncLifetime
{
    internal const string DatabaseName = AzureDataExplorerExporterOptions.DefaultDatabaseName;
    internal const string MappedLogsTableName = "OTELLogsMapped";
    internal const string MappedLogsMappingName = "otel_logs_mapping";

    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WarmUpRetryDelay = TimeSpan.FromSeconds(2);

    private KustoTestClient? client;

    public KustoConnectionStringBuilder ConnectionStringBuilder => new(this.TypedContainer.GetConnectionString());

    internal KustoTestClient Client => this.client ?? throw new InvalidOperationException("The Kusto emulator is not running.");

    protected override string DockerfileName => "kusto.Dockerfile";

    public override async ValueTask InitializeAsync()
    {
        if (!DockerHelper.IsAvailable(DockerPlatform.Linux))
        {
            return;
        }

        await this.StartAsync();

        this.client = new KustoTestClient(this.ConnectionStringBuilder, DatabaseName);

        // Streaming ingestion is not supported by the emulator for volatile databases, so persist it.
        await this.client.ExecuteControlCommandAsync(
            $".create database {DatabaseName} persist (@\"/kustodata/dbs/{DatabaseName}/md\", @\"/kustodata/dbs/{DatabaseName}/data\")",
            databaseName: "NetDefaultDB");

        foreach (var command in ReadmeKql.GetCommands())
        {
            await this.ExecuteSetupCommandAsync(command);
        }

        // A custom table with a JSON ingestion mapping. All tables are created up front because the
        // emulator's streaming ingestion path does not see tables created after it has been used.
        await this.client.ExecuteControlCommandAsync($".create-merge table {MappedLogsTableName} (Timestamp:datetime, Message:string, Severity:string, Service:string)");
        await this.client.ExecuteControlCommandAsync(
            $$$"""
            .create-or-alter table {{{MappedLogsTableName}}} ingestion json mapping '{{{MappedLogsMappingName}}}' '[{"column":"Timestamp","Properties":{"Path":"$.Timestamp"}},{"column":"Message","Properties":{"Path":"$.Body"}},{"column":"Severity","Properties":{"Path":"$.SeverityText"}},{"column":"Service","Properties":{"Path":"$.ResourceAttributes[\'service.name\']"}}]'
            """);

        // The README only shows enabling streaming ingestion for one table.
        await this.ExecuteSetupCommandAsync($".alter database {DatabaseName} policy streamingingestion enable");

        await this.WarmUpStreamingIngestionAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        this.client?.Dispose();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Configures the exporter to ingest into the emulator through <see cref="KustoRestIngestor"/>
    /// using managed (streaming) ingestion. The emulator does not support queued ingestion
    /// or Microsoft Entra authentication, so the ingestor is created without a token provider.
    /// </summary>
    internal void Configure(AzureDataExplorerExporterOptions options)
    {
        options.ClusterUri = new Uri(this.TypedContainer.GetConnectionString());
        options.ManagedIdentityId = "system";
        options.IngestionType = AzureDataExplorerIngestionType.Managed;
        options.RetryOnFailure = false;
        options.IngestorFactory = (table, mapping) => new KustoRestIngestor(
            KustoRestIngestor.DefaultHttpClient,
            tokenProvider: null,
            options.ClusterUri,
            options.DatabaseName,
            table,
            mapping,
            options.IngestionType);
    }

    protected override KustoContainer CreateContainer() => new KustoBuilder(this.GetImage()).Build();

    /// <summary>
    /// Runs a setup command, retrying while the freshly started emulator reports that streaming
    /// ingestion is not yet configured on the cluster.
    /// </summary>
    private async Task ExecuteSetupCommandAsync(string command)
    {
        var deadline = DateTime.UtcNow + WarmUpTimeout;
        while (true)
        {
            try
            {
                await this.Client.ExecuteControlCommandAsync(command);
                return;
            }
            catch (Exception ex) when (DateTime.UtcNow < deadline
                && ex.ToString().Contains("Streaming Ingestion not configured", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(WarmUpRetryDelay);
            }
        }
    }

    /// <summary>
    /// Streams a probe row into every table until each one succeeds so that tests don't race the
    /// emulator's streaming ingestion startup. Probe rows have no service name and are ignored by tests.
    /// </summary>
    private async Task WarmUpStreamingIngestionAsync()
    {
        string[] tables =
        [
            AzureDataExplorerExporterOptions.DefaultTracesTableName,
            AzureDataExplorerExporterOptions.DefaultMetricsTableName,
            AzureDataExplorerExporterOptions.DefaultLogsTableName,
            MappedLogsTableName,
        ];

        foreach (var table in tables)
        {
            var deadline = DateTime.UtcNow + WarmUpTimeout;
            while (true)
            {
                try
                {
                    using var ingestor = new KustoRestIngestor(
                        KustoRestIngestor.DefaultHttpClient,
                        tokenProvider: null,
                        new Uri(this.TypedContainer.GetConnectionString()),
                        DatabaseName,
                        table,
                        mappingReference: null,
                        AzureDataExplorerIngestionType.Managed);
                    using var data = new MemoryStream("{}\n"u8.ToArray());
                    await ingestor.IngestAsync(data, CancellationToken.None);
                    break;
                }
                catch (Exception) when (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(WarmUpRetryDelay);
                }
            }
        }
    }
}

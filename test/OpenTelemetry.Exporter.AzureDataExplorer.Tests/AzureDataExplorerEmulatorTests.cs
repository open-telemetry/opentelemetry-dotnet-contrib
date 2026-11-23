// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// End-to-end tests that ingest into the Kusto emulator (Kustainer) and query the rows back.
/// </summary>
[Collection(IntegrationTestsCollectionDefinition.Name)]
[Trait("CategoryName", "AzureDataExplorerEmulatorTests")]
public sealed class AzureDataExplorerEmulatorTests : IClassFixture<KustoEmulatorFixture>, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly KustoEmulatorFixture fixture;
    private readonly InMemoryEventListener listener = new(AzureDataExplorerExporterEventSource.Log, EventLevel.Warning);

    public AzureDataExplorerEmulatorTests(KustoEmulatorFixture fixture)
    {
        this.fixture = fixture;
    }

    public void Dispose() => this.listener.Dispose();

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task TracesAreIngestedUsingReadmeSchema()
    {
        await IntegrationScenarios.ExportAndVerifyTracesAsync(this.fixture.Client, this.Configure, Timeout, PollInterval);
        this.AssertNoExportErrors();
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task MetricsAreIngestedUsingReadmeSchema()
    {
        await IntegrationScenarios.ExportAndVerifyMetricsAsync(this.fixture.Client, this.Configure, Timeout, PollInterval);
        this.AssertNoExportErrors();
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task LogsAreIngestedUsingReadmeSchema()
    {
        await IntegrationScenarios.ExportAndVerifyLogsAsync(this.fixture.Client, this.Configure, Timeout, PollInterval);
        this.AssertNoExportErrors();
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task LogsAreIngestedUsingJsonMappingReference()
    {
        const string TableName = KustoEmulatorFixture.MappedLogsTableName;
        const string MappingName = KustoEmulatorFixture.MappedLogsMappingName;

        var client = this.fixture.Client;
        var serviceName = IntegrationScenarios.NewServiceName();
        using (var serviceProvider = IntegrationScenarios.CreateLoggingServiceProvider(serviceName, o =>
        {
            this.Configure(o);
            o.LogsTableName = TableName;
            o.LogsTableJsonMapping = MappingName;
        }))
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdxIntegration");
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Mapped {Value}", serviceName);
            }

            serviceProvider.GetRequiredService<LoggerProvider>().ForceFlush();
        }

        this.AssertNoExportErrors();

        var rows = await client.WaitForRowsAsync(
            $"{TableName} | where Message == {KustoTestClient.Quote("Mapped " + serviceName)}",
            expectedCount: 1,
            Timeout,
            PollInterval,
            TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(serviceName, row["Service"]);
        Assert.Equal("Info", row["Severity"]);
        Assert.IsType<DateTime>(row["Timestamp"]);
        this.AssertNoExportErrors();
    }

    private void Configure(AzureDataExplorerExporterOptions options) => this.fixture.Configure(options);

    private void AssertNoExportErrors()
    {
        var errors = this.listener.Events
            .Where(e => e.Level <= EventLevel.Error)
            .Select(e => string.Format(System.Globalization.CultureInfo.InvariantCulture, e.Message ?? e.EventName ?? string.Empty, e.Payload?.ToArray() ?? []))
            .ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}

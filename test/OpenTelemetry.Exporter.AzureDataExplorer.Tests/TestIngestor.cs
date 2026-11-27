// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

internal sealed class TestIngestor : IAzureDataExplorerIngestor
{
    private readonly Queue<Exception> failures = new();

    public List<string> Payloads { get; } = [];

    public string? TableName { get; set; }

    public string? Mapping { get; set; }

    public int Attempts { get; private set; }

    public int DisposeCount { get; private set; }

    public Func<CancellationToken, Task>? OnIngest { get; set; }

    public IEnumerable<JsonElement> Rows =>
        this.Payloads
            .SelectMany(p => p.Split(['\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(ParseRow);

    public static AzureDataExplorerExporterOptions CreateOptions(TestIngestor ingestor)
    {
        var options = new AzureDataExplorerExporterOptions();
        Configure(options, ingestor);
        return options;
    }

    public static void Configure(AzureDataExplorerExporterOptions options, TestIngestor ingestor)
    {
        options.ClusterUri = new Uri("https://mycluster.kusto.windows.net");
        options.ManagedIdentityId = "system";
        options.IngestorFactory = (table, mapping) =>
        {
            ingestor.TableName = table;
            ingestor.Mapping = mapping;
            return ingestor;
        };
    }

    public void FailWith(params Exception[] exceptions)
    {
        foreach (var exception in exceptions)
        {
            this.failures.Enqueue(exception);
        }
    }

    public async Task IngestAsync(Stream data, CancellationToken cancellationToken)
    {
        this.Attempts++;

        if (this.OnIngest != null)
        {
            await this.OnIngest(cancellationToken).ConfigureAwait(false);
        }

        if (this.failures.Count > 0)
        {
            throw this.failures.Dequeue();
        }

        using var reader = new StreamReader(data, Encoding.UTF8, false, 1024, leaveOpen: true);
        this.Payloads.Add(reader.ReadToEnd());
    }

    public void Dispose() => this.DisposeCount++;

    private static JsonElement ParseRow(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}

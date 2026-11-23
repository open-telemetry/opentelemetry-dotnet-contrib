// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// Thin wrapper over the Kusto query and admin clients used to verify ingested rows.
/// </summary>
internal sealed class KustoTestClient : IDisposable
{
    private readonly ICslAdminProvider admin;
    private readonly ICslQueryProvider query;

    public KustoTestClient(KustoConnectionStringBuilder connectionStringBuilder, string databaseName)
    {
        this.DatabaseName = databaseName;
        this.admin = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
        this.query = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
    }

    public string DatabaseName { get; }

    public static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    public async Task ExecuteControlCommandAsync(string command, string? databaseName = null)
    {
        using var reader = await this.admin.ExecuteControlCommandAsync(databaseName ?? this.DatabaseName, command, new ClientRequestProperties()).ConfigureAwait(false);
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string kql, CancellationToken cancellationToken)
    {
        using var reader = await this.query.ExecuteQueryAsync(this.DatabaseName, kql, new ClientRequestProperties(), cancellationToken).ConfigureAwait(false);

        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[reader.GetName(i)] = value is DBNull ? null : value;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Polls until the query returns at least <paramref name="expectedCount"/> rows or the timeout elapses.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> WaitForRowsAsync(
        string kql,
        int expectedCount,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var rows = await this.QueryAsync(kql, cancellationToken).ConfigureAwait(false);
            if (rows.Count >= expectedCount)
            {
                return rows;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                Assert.Fail($"Expected at least {expectedCount} row(s) within {timeout} but found {rows.Count} for query: {kql}");
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        this.admin.Dispose();
        this.query.Dispose();
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Abstraction over a Kusto ingest client bound to a single database and table.
/// </summary>
internal interface IAzureDataExplorerIngestor : IDisposable
{
    /// <summary>
    /// Ingests newline-delimited JSON rows.
    /// </summary>
    /// <param name="data">Uncompressed newline-delimited JSON payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the data has been accepted for ingestion.</returns>
    Task IngestAsync(Stream data, CancellationToken cancellationToken);
}

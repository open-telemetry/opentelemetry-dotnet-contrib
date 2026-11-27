// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Describes how telemetry is ingested into Azure Data Explorer.
/// </summary>
public enum AzureDataExplorerIngestionType
{
    /// <summary>
    /// Queued (batched) ingestion. Data is uploaded to storage managed by the
    /// cluster's data management service and ingested asynchronously. This is
    /// the default and is recommended for high volumes.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Managed streaming ingestion. Data is sent using streaming ingestion and
    /// transient failures or payloads too large for streaming automatically
    /// fall back to queued ingestion. Requires streaming ingestion to be enabled
    /// on the cluster and target tables.
    /// </summary>
    Managed = 1,
}

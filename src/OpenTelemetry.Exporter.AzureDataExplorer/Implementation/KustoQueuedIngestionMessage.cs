// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// The ingestion message posted to the data management service's queue for queued ingestion.
/// </summary>
internal sealed class KustoQueuedIngestionMessage
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("BlobPath")]
    public string? BlobPath { get; set; }

    [JsonPropertyName("RawDataSize")]
    public long RawDataSize { get; set; }

    [JsonPropertyName("DatabaseName")]
    public string? DatabaseName { get; set; }

    [JsonPropertyName("TableName")]
    public string? TableName { get; set; }

    [JsonPropertyName("RetainBlobOnSuccess")]
    public bool RetainBlobOnSuccess { get; set; }

    [JsonPropertyName("FlushImmediately")]
    public bool FlushImmediately { get; set; }

    [JsonPropertyName("IgnoreSizeLimit")]
    public bool IgnoreSizeLimit { get; set; }

    [JsonPropertyName("ReportLevel")]
    public int ReportLevel { get; set; }

    [JsonPropertyName("ReportMethod")]
    public int ReportMethod { get; set; }

    [JsonPropertyName("SourceMessageCreationTime")]
    public DateTime SourceMessageCreationTime { get; set; }

    [JsonPropertyName("AdditionalProperties")]
    public Dictionary<string, string>? AdditionalProperties { get; set; }
}

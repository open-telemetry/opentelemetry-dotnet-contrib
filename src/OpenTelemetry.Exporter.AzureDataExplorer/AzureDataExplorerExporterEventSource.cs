// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// EventSource for the Azure Data Explorer exporter.
/// </summary>
[EventSource(Name = "OpenTelemetry-Exporter-AzureDataExplorer")]
internal sealed class AzureDataExplorerExporterEventSource : EventSource
{
    public static readonly AzureDataExplorerExporterEventSource Log = new();

    private AzureDataExplorerExporterEventSource()
    {
    }

    [NonEvent]
    public void ExportFailed(string signal, string tableName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportFailed(signal, tableName, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void ExportRetrying(string signal, string tableName, int attempt, TimeSpan delay, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.ExportRetrying(signal, tableName, attempt, (long)delay.TotalMilliseconds, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void SerializationFailed(string signal, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.SerializationFailed(signal, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void IngestorDisposeFailed(string signal, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.IngestorDisposeFailed(signal, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void StreamingIngestionFallback(string tableName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.StreamingIngestionFallback(tableName, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void StreamingIngestionRetrying(string tableName, int attempt, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.StreamingIngestionRetrying(tableName, attempt, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to export {0} to table '{1}': {2}", Level = EventLevel.Error)]
    public void ExportFailed(string signal, string tableName, string exception)
        => this.WriteEvent(1, signal, tableName, exception);

    [Event(2, Message = "Export of {0} to table '{1}' failed on attempt {2}; retrying in {3} ms: {4}", Level = EventLevel.Warning)]
    public void ExportRetrying(string signal, string tableName, int attempt, long delayMilliseconds, string exception)
        => this.WriteEvent(2, signal, tableName, attempt, delayMilliseconds, exception);

    [Event(3, Message = "Failed to serialize {0}: {1}", Level = EventLevel.Error)]
    public void SerializationFailed(string signal, string exception)
        => this.WriteEvent(3, signal, exception);

    [Event(4, Message = "Unsupported metric type '{1}' for metric '{0}'; metric was skipped.", Level = EventLevel.Warning)]
    public void UnsupportedMetricType(string metricName, string metricType)
        => this.WriteEvent(4, metricName, metricType);

    [Event(5, Message = "Dropped row '{1}' of metric '{0}' because its value is not a finite number.", Level = EventLevel.Warning)]
    public void NonFiniteMetricValueDropped(string metricName, string metricRowName)
        => this.WriteEvent(5, metricName, metricRowName);

    [Event(6, Message = "Failed to dispose ingest client for {0}: {1}", Level = EventLevel.Warning)]
    public void IngestorDisposeFailed(string signal, string exception)
        => this.WriteEvent(6, signal, exception);

    [Event(7, Message = "Streaming ingestion into table '{0}' fell back to queued ingestion: {1}", Level = EventLevel.Warning)]
    public void StreamingIngestionFallback(string tableName, string reason)
        => this.WriteEvent(7, tableName, reason);

    [Event(8, Message = "Streaming ingestion into table '{0}' failed on attempt {1}; retrying: {2}", Level = EventLevel.Warning)]
    public void StreamingIngestionRetrying(string tableName, int attempt, string exception)
        => this.WriteEvent(8, tableName, attempt, exception);

    [Event(9, Message = "{0} was rejected as unauthorized; retrying once with a newly acquired token.", Level = EventLevel.Warning)]
    public void UnauthorizedRequestRetrying(string operation)
        => this.WriteEvent(9, operation);

    [Event(10, Message = "Streaming ingestion is unavailable for table '{0}'; using queued ingestion for the next {1} minutes.", Level = EventLevel.Warning)]
    public void StreamingIngestionSuspended(string tableName, int minutes)
        => this.WriteEvent(10, tableName, minutes);
}

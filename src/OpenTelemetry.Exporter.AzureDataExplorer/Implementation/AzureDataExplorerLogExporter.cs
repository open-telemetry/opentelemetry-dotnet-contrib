// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class AzureDataExplorerLogExporter : AzureDataExplorerExporterBase<LogRecord>
{
    private readonly AdxLogSerializer serializer = new();

    public AzureDataExplorerLogExporter(AzureDataExplorerExporterOptions options)
        : base(options, "logs", options?.LogsTableName!, options?.LogsTableJsonMapping)
    {
    }

    protected override void Serialize(JsonRowWriter rows, LogRecord item, AttributeMap resourceAttributes)
        => this.serializer.Write(rows, item, resourceAttributes);
}

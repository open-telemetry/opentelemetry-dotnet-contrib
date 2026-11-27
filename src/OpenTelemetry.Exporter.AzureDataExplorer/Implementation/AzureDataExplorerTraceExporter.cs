// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class AzureDataExplorerTraceExporter : AzureDataExplorerExporterBase<Activity>
{
    private readonly AdxTraceSerializer serializer = new();

    public AzureDataExplorerTraceExporter(AzureDataExplorerExporterOptions options)
        : base(options, "traces", options?.TracesTableName!, options?.TracesTableJsonMapping)
    {
    }

    protected override void Serialize(JsonRowWriter rows, Activity item, AttributeMap resourceAttributes)
        => this.serializer.Write(rows, item, resourceAttributes);
}

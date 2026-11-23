// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class AzureDataExplorerMetricExporter : AzureDataExplorerExporterBase<Metric>
{
    private readonly AdxMetricSerializer serializer = new();
    private string host = Environment.MachineName;

    public AzureDataExplorerMetricExporter(AzureDataExplorerExporterOptions options)
        : base(options, "metrics", options?.MetricsTableName!, options?.MetricsTableJsonMapping)
    {
    }

    protected override void Serialize(JsonRowWriter rows, Metric item, AttributeMap resourceAttributes)
        => this.serializer.Write(rows, item, resourceAttributes, this.host);

    protected override void OnResourceResolved(Resource resource)
        => this.host = AdxMetricSerializer.GetHost(resource.Attributes);
}

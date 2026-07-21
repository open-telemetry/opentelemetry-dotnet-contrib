// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using InfluxDB.Client;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBMetricsExporter : BaseExporter<Metric>
{
    private readonly IMetricsWriter writer;
    private readonly InfluxDBClient influxDbClient;
    private readonly WriteApiAsync writeApiAsync;

    public InfluxDBMetricsExporter(IMetricsWriter writer, InfluxDBClient influxDbClient)
    {
        this.writer = writer;
        this.influxDbClient = influxDbClient;
        this.writeApiAsync = influxDbClient.GetWriteApiAsync();
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            List<string> lineProtocol = [];
            foreach (var metric in batch)
            {
                this.writer.Write(metric, this.ParentProvider?.GetResource(), lineProtocol);
            }

            if (lineProtocol.Count == 0)
            {
                return ExportResult.Success;
            }

            this.writeApiAsync.WriteRecordsAsync(lineProtocol).GetAwaiter().GetResult();
            return ExportResult.Success;
        }
        catch (Exception exception)
        {
            InfluxDBEventSource.Log.FailedToExport(exception.Message);
            return ExportResult.Failure;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.influxDbClient.Dispose();
        }

        base.Dispose(disposing);
    }
}

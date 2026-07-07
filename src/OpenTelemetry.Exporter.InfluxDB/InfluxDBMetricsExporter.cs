// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using InfluxDB.Client;
using InfluxDB.Client.Writes;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBMetricsExporter : BaseExporter<Metric>
{
    private readonly IMetricsWriter writer;
    private readonly InfluxDBClient influxDbClient;
    private readonly WriteApi writeApi;

    // WriteApi writes are fire-and-forget into an unbounded Rx buffer and never
    // surface failures to the caller (backpressure is not implemented in the C#
    // client). We track the outcome of those asynchronous writes here so that
    // Export can stop feeding the buffer and report Failure while InfluxDB is
    // unreachable. Because the reader uses cumulative temporality, returning
    // Failure loses no data: the SDK keeps the accumulated values and re-exports
    // them once writes succeed again.
    private volatile bool writeApiHealthy = true;

    public InfluxDBMetricsExporter(IMetricsWriter writer, InfluxDBClient influxDbClient, WriteApi writeApi)
    {
        this.writer = writer;
        this.influxDbClient = influxDbClient;
        this.writeApi = writeApi;

        this.writeApi.EventHandler += (_, args) =>
        {
            switch (args)
            {
                case WriteSuccessEvent:
                    this.writeApiHealthy = true;
                    break;
                case WriteErrorEvent writeErrorEvent:
                    this.writeApiHealthy = false;
                    InfluxDBEventSource.Log.FailedToExport(writeErrorEvent.Exception.Message);
                    break;
                case WriteRetriableErrorEvent writeRetriableErrorEvent:
                    this.writeApiHealthy = false;
                    InfluxDBEventSource.Log.FailedToExport(writeRetriableErrorEvent.Exception.Message);
                    break;
                default:
                    break;
            }
        };
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        // If previous asynchronous writes failed, avoid enqueuing more points
        // into the unbounded WriteApi buffer (which would grow until OOM) and
        // let the PeriodicExportingMetricReader apply backpressure by retrying
        // on the next export cycle. Cumulative temporality guarantees no data is
        // lost in the meantime.
        if (!this.writeApiHealthy)
        {
            return ExportResult.Failure;
        }

        try
        {
            foreach (var metric in batch)
            {
                this.writer.Write(metric, this.ParentProvider?.GetResource(), this.writeApi);
            }

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
            this.writeApi.Dispose();
            this.influxDbClient.Dispose();
        }

        base.Dispose(disposing);
    }
}

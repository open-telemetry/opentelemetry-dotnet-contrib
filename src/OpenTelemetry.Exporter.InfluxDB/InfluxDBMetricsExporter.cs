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
    // client). We track the outcome of those asynchronous writes so that Export
    // can report Failure and stop feeding the buffer while InfluxDB is
    // unreachable. Because the reader uses cumulative temporality, returning
    // Failure loses no data: the SDK keeps the accumulated values and re-exports
    // them once writes succeed again.
    private volatile bool writeApiHealthy = true;

    // Set while a batch has been handed to WriteApi but its outcome has not been
    // observed yet. While writes are failing we only allow a single such batch to
    // be in flight at a time: it acts as a probe that lets us detect recovery
    // (a WriteSuccessEvent) without piling further points onto the buffer.
    private volatile bool writeInFlight;

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
                    this.writeInFlight = false;
                    break;
                case WriteErrorEvent writeErrorEvent:
                    this.writeApiHealthy = false;
                    this.writeInFlight = false;
                    InfluxDBEventSource.Log.FailedToExport(writeErrorEvent.Exception.Message);
                    break;
                case WriteRetriableErrorEvent writeRetriableErrorEvent:
                    // The client will retry this batch, so it is still in flight.
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
        // While writes are failing, avoid enqueuing more points into the unbounded
        // WriteApi buffer (which would otherwise grow until the process is
        // OOM-killed). We still let a single probe batch stay in flight so that a
        // subsequent WriteSuccessEvent can signal recovery. Cumulative temporality
        // guarantees no data is lost: the SDK keeps the accumulated values and the
        // PeriodicExportingMetricReader retries on the next export cycle.
        if (!this.writeApiHealthy && this.writeInFlight)
        {
            return ExportResult.Failure;
        }

        try
        {
            foreach (var metric in batch)
            {
                this.writer.Write(metric, this.ParentProvider?.GetResource(), this.writeApi);
            }

            this.writeInFlight = true;

            // Report the last observed health: on the first failing cycle this is
            // still Success, but as soon as the asynchronous failure is observed
            // every subsequent cycle reports Failure until writes recover.
            return this.writeApiHealthy ? ExportResult.Success : ExportResult.Failure;
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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using InfluxDB.Client;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBMetricsExporter : BaseExporter<Metric>
{
    private readonly IMetricsWriter writer;
    private readonly InfluxDBClient influxDbClient;
    private readonly WriteApi? writeApi;
    private readonly InfluxDBBackpressureWorker? backpressureWorker;
    private readonly InfluxDBMetricsExporterOptions options;

    public InfluxDBMetricsExporter(
        IMetricsWriter writer,
        InfluxDBClient influxDbClient,
        WriteApi? writeApi,
        IWriteApiAsync? writeApiAsync,
        InfluxDBMetricsExporterOptions options)
        : this(
            writer,
            influxDbClient,
            writeApi,
            writeApiAsync,
            options,
            payloadWriter: null)
    {
    }

    internal InfluxDBMetricsExporter(
        IMetricsWriter writer,
        InfluxDBClient influxDbClient,
        WriteApi? writeApi,
        IWriteApiAsync? writeApiAsync,
        InfluxDBMetricsExporterOptions options,
        IInfluxDBExportPayloadWriter? payloadWriter)
    {
        this.writer = writer;
        this.influxDbClient = influxDbClient;
        this.writeApi = writeApi;
        this.options = options;

        this.writeApi?.EventHandler += (_, args) =>
        {
            if (args.GetType().Name == "WriteErrorEvent")
            {
                var exception = args.GetType().GetProperty("Exception")?.GetValue(args) as Exception;
                if (exception != null)
                {
                    InfluxDBEventSource.Log.FailedToExport(exception.Message);
                }
            }
        };

        if (writeApiAsync != null || payloadWriter != null)
        {
            this.backpressureWorker = new InfluxDBBackpressureWorker(
                options.MaxPendingExports,
                options.BackpressureMode,
                payloadWriter ?? new InfluxDBExportPayloadWriter(writeApiAsync!),
                exception => InfluxDBEventSource.Log.FailedToExport(exception.Message));
        }
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

            if (this.backpressureWorker != null)
            {
                var enqueued = this.backpressureWorker.Enqueue(lineProtocol, out var droppedWriteCount);
                if (droppedWriteCount > 0)
                {
                    InfluxDBEventSource.Log.MetricWritesDropped(droppedWriteCount, this.options.BackpressureMode.ToString());
                }

                Debug.Assert(enqueued || droppedWriteCount > 0, "Enqueue returned false without dropping any payload.");

                return enqueued || droppedWriteCount > 0
                    ? ExportResult.Success
                    : ExportResult.Failure;
            }

            this.writeApi!.WriteRecords(lineProtocol);
            return ExportResult.Success;
        }
        catch (Exception exception)
        {
            InfluxDBEventSource.Log.FailedToExport(exception.Message);
            return ExportResult.Failure;
        }
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        if (this.backpressureWorker != null)
        {
            return this.backpressureWorker.Flush(timeoutMilliseconds);
        }

        this.writeApi?.Flush();
        return true;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        if (this.backpressureWorker != null)
        {
            return this.backpressureWorker.Flush(timeoutMilliseconds);
        }

        this.writeApi?.Flush();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Shutdown(this.options.TimeoutMilliseconds);
            this.backpressureWorker?.Dispose();
            this.writeApi?.Dispose();
            this.influxDbClient.Dispose();
        }

        base.Dispose(disposing);
    }
}

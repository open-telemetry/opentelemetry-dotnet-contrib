// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal class OtlpProtobufMetricExporter : BaseExporter<Metric>
{
    private const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly OtlpProtobufSerializer otlpProtobufSerializer;

    private Resource resource;
    private string defaultMonitoringAccount;
    private string defaultMetricNamespace;

    internal Resource MetricResource => this.resource ??= this.ParentProvider.GetResource();

    public OtlpProtobufMetricExporter(GenevaMetricExporterOptions options)
    {
        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        this.defaultMonitoringAccount = connectionStringBuilder.Account;
        this.defaultMetricNamespace = connectionStringBuilder.Namespace;
        this.otlpProtobufSerializer = new OtlpProtobufSerializer();
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        var result = ExportResult.Success;

        int currentPosition = this.buffer.Length;

        try
        {
            this.otlpProtobufSerializer.SerializeMetrics(this.buffer, ref currentPosition, this.MetricResource, this.defaultMonitoringAccount, this.defaultMetricNamespace, batch);

            // Send request.
            MetricEtwDataTransport.Instance.SendOtlpProtobufEvent(this.buffer, currentPosition);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("metric batch failed", ex);
        }

        return result;
    }
}

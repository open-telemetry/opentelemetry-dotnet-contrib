// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenTelemetry.Exporter.Geneva.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaMetricExporter : BaseExporter<Metric>
{
    internal const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    internal const int MaxDimensionNameSize = 256;

    internal const int MaxDimensionValueSize = 1024;

    internal const string DimensionKeyForCustomMonitoringAccount = "_microsoft_metrics_account";

    internal const string DimensionKeyForCustomMetricsNamespace = "_microsoft_metrics_namespace";

    internal readonly TlvMetricExporter Exporter;

    private delegate ExportResult ExportMetricsFunc(in Batch<Metric> batch);

    private readonly ExportMetricsFunc exportMetrics;

    private bool isDisposed;

    public GenevaMetricExporter(GenevaMetricExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        // TODO: parse connection string to check if otlp protobuf format is enabled.
        // and then enable either TLV Exporter or Protobuf based exporter.
        var tlvMetricsExporter = new TlvMetricExporter(options);

        this.exportMetrics = tlvMetricsExporter.Export;

        this.Exporter = tlvMetricsExporter;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        return this.exportMetrics(batch);
    }

    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            this.Exporter.Dispose();
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }

    internal static PropertyInfo GetOpenTelemetryInstrumentNameRegexProperty()
    {
        var meterProviderBuilderSdkType = typeof(Sdk).Assembly.GetType("OpenTelemetry.Metrics.MeterProviderBuilderSdk", throwOnError: false)
            ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk type could not be found reflectively.");

        var instrumentNameRegexProperty = meterProviderBuilderSdkType.GetProperty("InstrumentNameRegex", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk.InstrumentNameRegex property could not be found reflectively.");

        return instrumentNameRegexProperty;
    }

    internal static void DisableOpenTelemetrySdkMetricNameValidation()
    {
        GetOpenTelemetryInstrumentNameRegexProperty().SetValue(null, new Regex(".*", RegexOptions.Compiled));
    }
}

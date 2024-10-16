// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;
using OpenTelemetry.Exporter.Geneva.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// An exporter for Geneva metrics.
/// </summary>
public partial class GenevaMetricExporter : BaseExporter<Metric>
{
    internal const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    internal const int MaxDimensionNameSize = 256;

    internal const int MaxDimensionValueSize = 1024;

    internal const string DimensionKeyForCustomMonitoringAccount = "_microsoft_metrics_account";

    internal const string DimensionKeyForCustomMetricsNamespace = "_microsoft_metrics_namespace";

    private const string DisableRegexPattern = ".*";

    private readonly IDisposable exporter;

    private readonly ExportMetricsFunc exportMetrics;

    private bool isDisposed;

    private Resource? resource;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenevaMetricExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="GenevaMetricExporterOptions"/>.</param>
    public GenevaMetricExporter(GenevaMetricExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);

        if (connectionStringBuilder.DisableMetricNameValidation)
        {
            DisableOpenTelemetrySdkMetricNameValidation();
        }

        if (connectionStringBuilder.PrivatePreviewEnableOtlpProtobufEncoding)
        {
            var otlpProtobufExporter = new OtlpProtobufMetricExporter(
                () => { return this.Resource; },
                connectionStringBuilder,
                options.PrepopulatedMetricDimensions);

            this.exporter = otlpProtobufExporter;

            this.exportMetrics = otlpProtobufExporter.Export;
        }
        else
        {
            var tlvMetricsExporter = new TlvMetricExporter(connectionStringBuilder, options.PrepopulatedMetricDimensions);

            this.exportMetrics = tlvMetricsExporter.Export;

            this.exporter = tlvMetricsExporter;
        }
    }

    private delegate ExportResult ExportMetricsFunc(in Batch<Metric> batch);

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        return this.exportMetrics(batch);
    }

    internal static PropertyInfo GetOpenTelemetryInstrumentNameRegexProperty()
    {
        var meterProviderBuilderSdkType = Type.GetType("OpenTelemetry.Metrics.MeterProviderBuilderSdk, OpenTelemetry", throwOnError: false)
            ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk type could not be found reflectively.");

        var instrumentNameRegexProperty = meterProviderBuilderSdkType.GetProperty("InstrumentNameRegex", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("OpenTelemetry.Metrics.MeterProviderBuilderSdk.InstrumentNameRegex property could not be found reflectively.");

        return instrumentNameRegexProperty;
    }

    internal static void DisableOpenTelemetrySdkMetricNameValidation()
    {
        GetOpenTelemetryInstrumentNameRegexProperty().SetValue(null, GetDisableRegexPattern());
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            this.exporter.Dispose();
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }

#if NET
    [GeneratedRegex(DisableRegexPattern)]
    private static partial Regex GetDisableRegexPattern();
#else
    private static Regex GetDisableRegexPattern() => new(DisableRegexPattern, RegexOptions.Compiled);
#endif
}

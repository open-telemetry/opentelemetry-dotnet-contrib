// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Google.Api;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenTelemetry.Exporter.GoogleCloud.Implementation;
using OpenTelemetry.Metrics;
using Metric = OpenTelemetry.Metrics.Metric;

namespace OpenTelemetry.Exporter.GoogleCloud;

/// <summary>
/// Exports a metrics to Google Cloud Monitoring .
/// </summary>
public class GoogleCloudMonitoringExporter : BaseExporter<Metric>
{
    private readonly ProjectName projectName;
    private readonly MetricServiceSettings? metricServiceSettings;
    private MetricServiceClient? metricServiceClient;

#pragma warning disable CA1810 // Initialize reference type static fields inline
    static GoogleCloudMonitoringExporter()
#pragma warning restore CA1810 // Initialize reference type static fields inline
    {
        try
        {
            var assemblyPackageVersion = typeof(GoogleCloudTraceExporter).GetTypeInfo().Assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            GoogleCloudMetricExportVersion = assemblyPackageVersion;
        }
        catch (Exception)
        {
            GoogleCloudMetricExportVersion = $"{Constants.PackagVersionUndefined}";
        }

        try
        {
            OpenTelemetryExporterVersion = Assembly.GetCallingAssembly().GetName().Version!.ToString();
        }
        catch (Exception)
        {
            OpenTelemetryExporterVersion = $"{Constants.PackagVersionUndefined}";
        }
    }

    private static string OpenTelemetryExporterVersion { get; set; }

    private static string GoogleCloudMetricExportVersion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCloudMonitoringExporter"/> class.
    /// </summary>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    public GoogleCloudMonitoringExporter(string projectId)
    {
        this.projectName = new ProjectName(projectId);

        // Set header mutation for every outgoing API call to Google Cloud Monitoring so the BE knows
        // which version of OC client is calling it as well as which version of the exporter
        var callSettings = CallSettings.FromHeaderMutation(StackdriverCallHeaderAppender);
        this.metricServiceSettings = new MetricServiceSettings { CallSettings = callSettings };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleCloudMonitoringExporter"/> class.
    /// Only used internally for tests.
    /// </summary>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    /// <param name="metricServiceClient">MetricServiceClient instance to use.</param>
    [ExcludeFromCodeCoverage]
    internal GoogleCloudMonitoringExporter(string projectId, MetricServiceClient metricServiceClient)
        : this(projectId)
    {
        this.metricServiceClient = metricServiceClient;
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        this.metricServiceClient ??=
            new MetricServiceClientBuilder { Settings = this.metricServiceSettings, }.Build();
        var timeSeriesList = new List<TimeSeries>();

        foreach (var metric in batch)
        {
            foreach (var point in metric.GetMetricPoints())
            {
                var ts = CreateTimeSeriesForMetricPoint(metric, point);
                timeSeriesList.Add(ts);
            }
        }

        if (timeSeriesList.Count > 0)
        {
            this.metricServiceClient.CreateTimeSeries(this.projectName, timeSeriesList);
        }

        return ExportResult.Success;
    }

    private static TimeSeries CreateTimeSeriesForMetricPoint(Metric metric, MetricPoint point)
    {
        MetricDescriptor.Types.MetricKind metricKind;
        MetricDescriptor.Types.ValueType valueType;
        TypedValue value;

        switch (metric.MetricType)
        {
            case MetricType.DoubleGauge:
                metricKind = MetricDescriptor.Types.MetricKind.Gauge;
                valueType = MetricDescriptor.Types.ValueType.Double;
                value = new TypedValue { DoubleValue = point.GetGaugeLastValueDouble() };
                break;
            case MetricType.LongGauge:
                metricKind = MetricDescriptor.Types.MetricKind.Gauge;
                valueType = MetricDescriptor.Types.ValueType.Int64;
                value = new TypedValue { Int64Value = point.GetGaugeLastValueLong() };
                break;
            case MetricType.DoubleSum:
                metricKind = MetricDescriptor.Types.MetricKind.Cumulative;
                valueType = MetricDescriptor.Types.ValueType.Double;
                value = new TypedValue { DoubleValue = point.GetSumDouble() };
                break;
            case MetricType.LongSum:
                metricKind = MetricDescriptor.Types.MetricKind.Cumulative;
                valueType = MetricDescriptor.Types.ValueType.Int64;
                value = new TypedValue { Int64Value = point.GetSumLong() };
                break;
            case MetricType.DoubleSumNonMonotonic:
                metricKind = MetricDescriptor.Types.MetricKind.Gauge;
                valueType = MetricDescriptor.Types.ValueType.Double;
                value = new TypedValue { DoubleValue = point.GetSumDouble() };
                break;
            case MetricType.LongSumNonMonotonic:
                metricKind = MetricDescriptor.Types.MetricKind.Gauge;
                valueType = MetricDescriptor.Types.ValueType.Int64;
                value = new TypedValue { Int64Value = point.GetSumLong() };
                break;
            case MetricType.Histogram:
                metricKind = MetricDescriptor.Types.MetricKind.Cumulative;
                valueType = MetricDescriptor.Types.ValueType.Distribution;
                var count = point.GetHistogramCount();
                var sum = point.GetHistogramSum();
                var mean = count > 0 ? sum / count : 0;
                var buckets = point.GetHistogramBuckets();

                value = new TypedValue
                {
                    DistributionValue = new Distribution
                    {
                        Count = count,
                        Mean = mean,
                        BucketCounts = { ToRepeatedField(buckets) },
                        BucketOptions = new Distribution.Types.BucketOptions
                        {
                            ExplicitBuckets = new Distribution.Types.BucketOptions.Types.Explicit
                            {
                                Bounds = { ToRepeatedFieldBounds(buckets) },
                            },
                        },
                    },
                };
                break;
            case MetricType.ExponentialHistogram:
                throw new NotImplementedException("ExponentialHistogram is not supported yet.");
            default:
                metricKind = MetricDescriptor.Types.MetricKind.Gauge;
                valueType = MetricDescriptor.Types.ValueType.Double;
                value = new TypedValue { DoubleValue = point.GetSumDouble() };
                break;
        }

        return new TimeSeries
        {
            Metric = new Google.Api.Metric { Type = $"custom.googleapis.com/opentelemetry/{metric.Name}" },
            Resource = new MonitoredResource { Type = "global" },
            MetricKind = metricKind,
            ValueType = valueType,
            Points =
            {
                new Point
                {
                    Interval = new TimeInterval
                    {
                        StartTime = Timestamp.FromDateTimeOffset(point.StartTime),
                        EndTime = Timestamp.FromDateTimeOffset(point.EndTime),
                    },
                    Value = value,
                },
            },
        };
    }

    private static RepeatedField<long> ToRepeatedField(HistogramBuckets? buckets)
    {
        var result = new RepeatedField<long>();
        if (buckets == null)
        {
            return result;
        }

        foreach (var count in buckets)
        {
            result.Add(count.BucketCount);
        }

        return result;
    }

    private static RepeatedField<double> ToRepeatedFieldBounds(HistogramBuckets? buckets)
    {
        var result = new RepeatedField<double>();
        if (buckets == null)
        {
            return result;
        }

        foreach (var bucket in buckets)
        {
            result.Add(bucket.ExplicitBound);
        }

        return result;
    }

    /// <summary>
    /// Appends OpenTelemetry headers for every outgoing request to Stackdriver Backend.
    /// </summary>
    /// <param name="metadata">The metadata that is sent with every outgoing http request.</param>
    private static void StackdriverCallHeaderAppender(Metadata metadata)
    {
        metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
        metadata.Add(
            "AGENT_LABEL_VALUE_STRING",
            $"{OpenTelemetryExporterVersion}; googlecloud-exporter {GoogleCloudMetricExportVersion}");
    }
}

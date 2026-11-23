// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Serializes metrics into rows matching the Collector exporter's <c>AdxMetric</c> schema.
/// </summary>
internal sealed class AdxMetricSerializer
{
    internal const string HostKey = "host.name";

    private const string SumDescription = "(Sum total of samples)";
    private const string CountDescription = "(Count of samples)";
    private const string MinDescription = "(Minimum value observed)";
    private const string MaxDescription = "(Maximum value observed)";

    private readonly AttributeMap attributes = new();

    public static string GetHost(IEnumerable<KeyValuePair<string, object>> resourceAttributes)
    {
        foreach (var attribute in resourceAttributes)
        {
            if (attribute.Key == HostKey && attribute.Value is string host)
            {
                return host;
            }
        }

        return Environment.MachineName;
    }

    public void Write(JsonRowWriter rows, Metric metric, AttributeMap resourceAttributes, string host)
    {
        var metricType = metric.MetricType;

        // Exponential histograms also satisfy IsHistogram() but do not support
        // explicit buckets. The Collector exporter does not support them either.
        if (metricType == MetricType.ExponentialHistogram)
        {
            AzureDataExplorerExporterEventSource.Log.UnsupportedMetricType(metric.Name, metricType.ToString());
            return;
        }

        if (metricType.IsHistogram())
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                this.WriteHistogramPoint(rows, metric, in point, resourceAttributes, host);
            }

            return;
        }

        if (!metricType.IsSum() && !metricType.IsGauge())
        {
            AzureDataExplorerExporterEventSource.Log.UnsupportedMetricType(metric.Name, metricType.ToString());
            return;
        }

        var typeName = metricType.IsSum() ? "Sum" : "Gauge";
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            var value = metricType switch
            {
                MetricType.LongSum or MetricType.LongSumNonMonotonic => point.GetSumLong(),
                MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic => point.GetSumDouble(),
                MetricType.LongGauge => point.GetGaugeLastValueLong(),
                _ => point.GetGaugeLastValueDouble(),
            };

            this.WriteRow(rows, metric, in point, resourceAttributes, host, null, null, typeName, value, includeExemplars: true, bucketBound: null);
        }
    }

    private static void WriteExemplars(Utf8JsonWriter writer, MetricType metricType, ReadOnlyExemplarCollection exemplars)
    {
        var started = false;
        var isLong = metricType.IsLong();
        foreach (ref readonly var exemplar in exemplars)
        {
            if (!started)
            {
                writer.WriteStartArray("Exemplars");
                started = true;
            }

            writer.WriteStartObject();
            writer.WriteString("timestamp", AdxJson.FormatTimestamp(exemplar.Timestamp.UtcDateTime));
            writer.WriteString("trace_id", AdxJson.FormatTraceId(exemplar.TraceId));
            writer.WriteString("span_id", AdxJson.FormatSpanId(exemplar.SpanId));
            writer.WriteStartObject("attributes");
            foreach (var tag in exemplar.FilteredTags)
            {
                writer.WritePropertyName(tag.Key);
                AdxJson.WriteValue(writer, tag.Value);
            }

            writer.WriteEndObject();
            writer.WritePropertyName("value");
            if (isLong)
            {
                writer.WriteNumberValue(exemplar.LongValue);
            }
            else
            {
                AdxJson.WriteSafeDouble(writer, exemplar.DoubleValue);
            }

            writer.WriteEndObject();
        }

        if (started)
        {
            writer.WriteEndArray();
        }
    }

    private void WriteHistogramPoint(JsonRowWriter rows, Metric metric, in MetricPoint point, AttributeMap resourceAttributes, string host)
    {
        const string TypeName = "Histogram";
        var description = metric.Description ?? string.Empty;

        this.WriteRow(rows, metric, in point, resourceAttributes, host, metric.Name + "_sum", description + SumDescription, TypeName, point.GetHistogramSum(), includeExemplars: false, bucketBound: null);
        this.WriteRow(rows, metric, in point, resourceAttributes, host, metric.Name + "_count", description + CountDescription, TypeName, point.GetHistogramCount(), includeExemplars: true, bucketBound: null);

        if (point.TryGetHistogramMinMaxValues(out var min, out var max))
        {
            this.WriteRow(rows, metric, in point, resourceAttributes, host, metric.Name + "_min", description + MinDescription, TypeName, min, includeExemplars: false, bucketBound: null);
            this.WriteRow(rows, metric, in point, resourceAttributes, host, metric.Name + "_max", description + MaxDescription, TypeName, max, includeExemplars: false, bucketBound: null);
        }

        // The SDK always reports one more bucket than explicit bounds, with the
        // last bucket's bound being +Inf, which matches the Collector's requirement.
        ulong cumulative = 0;
        foreach (var bucket in point.GetHistogramBuckets())
        {
            cumulative += (ulong)bucket.BucketCount;
            this.WriteRow(rows, metric, in point, resourceAttributes, host, metric.Name + "_bucket", null, TypeName, cumulative, includeExemplars: false, bucketBound: bucket.ExplicitBound);
        }
    }

    private void WriteRow(
        JsonRowWriter rows,
        Metric metric,
        in MetricPoint point,
        AttributeMap resourceAttributes,
        string host,
        string? name,
        string? description,
        string typeName,
        double value,
        bool includeExemplars,
        double? bucketBound)
    {
        name = string.IsNullOrWhiteSpace(name) ? metric.Name : name;
        description = string.IsNullOrWhiteSpace(description) ? metric.Description ?? string.Empty : description;

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            // A non-finite value cannot be represented in the real-typed MetricValue
            // column; the Collector fails to encode such rows as well.
            AzureDataExplorerExporterEventSource.Log.NonFiniteMetricValueDropped(metric.Name, name!);
            return;
        }

        var writer = rows.BeginRow();
        try
        {
            writer.WriteStartObject();
            writer.WriteString("Timestamp", AdxJson.FormatTimestamp(point.EndTime.UtcDateTime));
            writer.WriteString("MetricName", name);
            writer.WriteString("MetricType", typeName);
            writer.WriteString("MetricUnit", metric.Unit ?? string.Empty);
            writer.WriteString("MetricDescription", description);
            writer.WriteNumber("MetricValue", value);

            this.attributes.Clear();
            this.attributes.SetScope(metric.MeterName, metric.MeterVersion);
            if (bucketBound.HasValue)
            {
                this.attributes.Set("le", AdxJson.FormatGoFloat(bucketBound.Value));
            }

            foreach (var tag in point.Tags)
            {
                this.attributes.Set(tag.Key, tag.Value);
            }

            writer.WritePropertyName("MetricAttributes");
            this.attributes.WriteTo(writer);
            writer.WriteString("Host", host);
            writer.WritePropertyName("ResourceAttributes");
            resourceAttributes.WriteTo(writer);

            if (includeExemplars && point.TryGetExemplars(out var exemplars))
            {
                WriteExemplars(writer, metric.MetricType, exemplars);
            }

            writer.WriteEndObject();
            rows.EndRow();
        }
        catch (Exception ex)
        {
            rows.AbortRow();
            AzureDataExplorerExporterEventSource.Log.SerializationFailed("metrics", ex);
        }
    }
}

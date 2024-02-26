// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

internal class FieldNumberConstants
{
    // MetricsData
    internal const int MetricsData_resource_metrics = 1;

    // ResourceMetrics
    internal const int ResourceMetrics_resource = 1;
    internal const int ResourceMetrics_scope_metrics = 2;
    internal const int ResourceMetrics_schema_url = 3;

    // ScopeMetrics
    internal const int ScopeMetrics_scope = 1;
    internal const int ScopeMetrics_metrics = 2;
    internal const int ScopeMetrics_schema_url = 3;

    // Metric
    internal const int Metric_name = 1;
    internal const int Metric_description = 2;
    internal const int Metric_unit = 3;
    internal const int Metric_gauge = 5;
    internal const int Metric_sum = 7;
    internal const int Metric_histogram = 9;
    internal const int Metric_exponential_histogram = 10;
    internal const int Metric_summary = 11;

    // Sum
    internal const int Sum_data_points = 1;
    internal const int Sum_aggregation_temporality = 2;
    internal const int Sum_is_monotonic = 3;

    // Gauge
    internal const int Gauge_data_points = 1;

    // Histogram
    internal const int Histogram_data_points = 1;
    internal const int Histogram_aggregation_temporality = 2;

    // ExponentialHistogram
    internal const int ExponentialHistogram_data_points = 1;
    internal const int ExponentialHistogram_aggregation_temporality = 2;

    // NumberDataPoint
    internal const int NumberDataPoint_attributes = 7;
    internal const int NumberDataPoint_start_time_unix_nano = 2;
    internal const int NumberDataPoint_time_unix_nano = 3;
    internal const int NumberDataPoint_as_int = 6;
    internal const int NumberDataPoint_as_double = 4;
    internal const int NumberDataPoint_exemplars = 5;
    internal const int NumberDataPoint_flags = 8;

    // HistogramDataPoint
    internal const int HistogramDataPoint_attributes = 9;
    internal const int HistogramDataPoint_start_time_unix_nano = 2;
    internal const int HistogramDataPoint_time_unix_nano = 3;
    internal const int HistogramDataPoint_count = 4;
    internal const int HistogramDataPoint_sum = 5;
    internal const int HistogramDataPoint_bucket_counts = 6;
    internal const int HistogramDataPoint_explicit_bounds = 7;
    internal const int HistogramDataPoint_exemplars = 8;
    internal const int HistogramDataPoint_flags = 10;
    internal const int HistogramDataPoint_min = 11;
    internal const int HistogramDataPoint_max = 12;

    // AnyValue
    internal const int AnyValue_string_value = 1;
    internal const int AnyValue_bool_value = 2;
    internal const int AnyValue_int_value = 3;
    internal const int AnyValue_double_value = 4;
    internal const int AnyValue_array_value = 5;
    internal const int AnyValue_kvlist_value = 6;
    internal const int AnyValue_bytes_value = 7;

    // ArrayValue
    internal const int ArrayValue_values = 1;

    // KeyValueList
    internal const int KeyValueList_values = 1;

    // KeyValue
    internal const int KeyValue_key = 1;
    internal const int KeyValue_value = 2;

    // InstrumentationScope
    internal const int InstrumentationScope_name = 1;
    internal const int InstrumentationScope_version = 2;
    internal const int InstrumentationScope_attributes = 3;
    internal const int InstrumentationScope_dropped_attributes_count = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetMetricTypeFieldNumber(MetricType metricType)
    {
        switch (metricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    return 7;
                }

            case MetricType.DoubleGauge:
            case MetricType.LongGauge:
                {
                    return 5;
                }

            case MetricType.Histogram:
                {
                    return 9;
                }

            case MetricType.ExponentialHistogram:
                {
                    return 10;
                }
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetAggregationTemporality(AggregationTemporality aggregationTemporality)
    {
        switch (aggregationTemporality)
        {
            case AggregationTemporality.Delta: return 1;
            case AggregationTemporality.Cumulative: return 2;
        }

        return 0;
    }
}

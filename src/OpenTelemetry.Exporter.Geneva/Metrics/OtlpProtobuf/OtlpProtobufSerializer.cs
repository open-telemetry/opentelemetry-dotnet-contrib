// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal class OtlpProtobufSerializer
{
    private static readonly ConcurrentBag<List<Metric>> MetricListPool = new();

    private readonly Dictionary<string, List<Metric>> scopeMetrics = new();

    internal void SerializeMetrics(byte[] buffer, ref int currentPosition, Resource resource, string metricAccount, string metricNamespace, in Batch<Metric> metricBatch)
    {
        foreach (var metric in metricBatch)
        {
            if (this.scopeMetrics.TryGetValue(metric.MeterName, out var metricList))
            {
                metricList.Add(metric);
            }
            else
            {
                if (MetricListPool.TryTake(out var newList))
                {
                    newList.Add(metric);
                    this.scopeMetrics[metric.MeterName] = newList;
                }
                else
                {
                    newList = new List<Metric>();
                    newList.Add(metric);
                    this.scopeMetrics[metric.MeterName] = newList;
                }
            }
        }

        // Serialize
        this.SerializeResourceMetrics(buffer, ref currentPosition, resource, metricAccount, metricNamespace);

        this.ReturnScopeMetrics();
    }

    internal void ReturnScopeMetrics()
    {
        foreach (var list in this.scopeMetrics)
        {
            list.Value.Clear();
            MetricListPool.Add(list.Value);
        }

        this.scopeMetrics.Clear();
    }

    internal void SerializeResourceMetrics(byte[] buffer, ref int currentPosition, Resource resource, string metricAccount, string metricNamespace)
    {
        int previousScopePosition = currentPosition;

        // write ScopeMetrics field number type length - field 2
        foreach (KeyValuePair<string, List<Metric>> metric in this.scopeMetrics)
        {
            // Serialize this meter/scope
            this.SerializeScopeMetrics(buffer, ref currentPosition, metric.Key, metric.Value);

            // write scope metrics tag and length {Repeated field}
            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousScopePosition - currentPosition, FieldNumberConstants.ResourceMetrics_scope_metrics, WireFormat.WireType.LengthDelimited);

            previousScopePosition = currentPosition;
        }

        SerializeResource(buffer, ref currentPosition, resource, metricAccount, metricNamespace);

        // Write for MetricsData field 1 {Repeated field} (After because writing from end to begin)
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, buffer.Length - currentPosition, FieldNumberConstants.ResourceMetrics_resource, WireFormat.WireType.LengthDelimited);

        // TODO: Serialize schema_url field
    }

    internal void SerializeScopeMetrics(byte[] buffer, ref int currentPosition, string scopeName, List<Metric> metrics)
    {
        int previousMetricPosition = currentPosition;

        IEnumerable<KeyValuePair<string, object>> meterTags = null;

        foreach (Metric metric in metrics)
        {
            if (metric.MeterTags != null && meterTags == null)
            {
                meterTags = metric.MeterTags;
            }

            // Serialize metrics for the meter/scope
            SerializeMetric(buffer, ref currentPosition, metric);

            // Write metrics tag and length {Repeated field}
            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousMetricPosition - currentPosition, FieldNumberConstants.ScopeMetrics_metrics, WireFormat.WireType.LengthDelimited);

            previousMetricPosition = currentPosition;
        }

        // Serialize instrumentation scope field
        SerializeInstrumentationScope(buffer, ref currentPosition, scopeName, meterTags);

        // TODO: Serialize schema_url field.
    }

    private static void SerializeMetric(byte[] buffer, ref int currentPosition, Metric metric)
    {
        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    int previousMetricPosition = currentPosition;

                    // Write isMonotonic tag
                    OtlpProtobufSerializerHelper.WriteBoolWithTag(buffer, ref currentPosition, FieldNumberConstants.Sum_is_monotonic, metric.MetricType == MetricType.LongSum);

                    // Write aggregationTemporality tag
                    OtlpProtobufSerializerHelper.WriteEnumWithTag(buffer, ref currentPosition, FieldNumberConstants.Sum_aggregation_temporality, FieldNumberConstants.GetAggregationTemporality(metric.Temporality));

                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        int previousMetricPointPosition = currentPosition;

                        var sum = (ulong)metricPoint.GetSumLong();

                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_as_int, sum);

                        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_start_time_unix_nano, startTime);

                        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_time_unix_nano, endTime);

                        SerializeTags(buffer, ref currentPosition, metricPoint.Tags, FieldNumberConstants.NumberDataPoint_attributes);

                        // TODO: exemplars.

                        // Write numberdatapoint {Repeated field}
                        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousMetricPointPosition - currentPosition, FieldNumberConstants.Sum_data_points, WireFormat.WireType.LengthDelimited);
                    }

                    // Write Sum tag
                    OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousMetricPosition - currentPosition, FieldNumberConstants.GetMetricTypeFieldNumber(metric.MetricType), WireFormat.WireType.LengthDelimited);

                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    // TODO
                    break;
                }

            case MetricType.LongGauge:
                {
                    // TODO
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    // TODO
                    break;
                }

            case MetricType.Histogram:
                {
                    // TODO
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    // TODO
                    break;
                }
        }

        // Write metric name
        OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, metric.Name, FieldNumberConstants.Metric_name);

        // Write metric description
        if (metric.Description != null)
        {
            OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, metric.Description, FieldNumberConstants.Metric_description);
        }

        // Write metric unit
        if (metric.Unit != null)
        {
            OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, metric.Unit, FieldNumberConstants.Metric_unit);
        }
    }

    internal static void SerializeInstrumentationScope(byte[] buffer, ref int currentPosition, string name, IEnumerable<KeyValuePair<string, object>> meterTags)
    {
        int previousPosition = currentPosition;

        // Write name
        OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, name, 1);

        SerializeTags(buffer, ref currentPosition, meterTags, FieldNumberConstants.InstrumentationScope_attributes);

        // Write instrumentation Scope Tag
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousPosition - currentPosition, FieldNumberConstants.ScopeMetrics_scope, WireFormat.WireType.LengthDelimited);
    }

    private static void SerializeTags(byte[] buffer, ref int currentPosition, IEnumerable<KeyValuePair<string, object>> attributes, int fieldNumber)
    {
        if (attributes != null)
        {
            foreach (var tag in attributes)
            {
                if (tag.Value != null)
                {
                    SerializeTag(buffer, ref currentPosition, tag.Key, tag.Value, fieldNumber);
                }
            }
        }
    }

    private static void SerializeResource(byte[] buffer, ref int currentPosition, Resource resource, string metricAccount, string metricNamespace)
    {
        // TODO
    }

    internal static void SerializeTags(byte[] buffer, ref int currentPosition, ReadOnlyTagCollection tags, int fieldNumber)
    {
        foreach (var tag in tags)
        {
            if (tag.Value != null)
            {
                SerializeTag(buffer, ref currentPosition, tag.Key, tag.Value, fieldNumber);
            }
        }
    }

    internal static void SerializeTag(byte[] buffer, ref int currentPosition, string key, object value, int fieldNumber)
    {
        int previousPosition = currentPosition;
        try
        {
            switch (value)
            {
                case char:
                case string:
                    OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, Convert.ToString(value, CultureInfo.InvariantCulture), FieldNumberConstants.AnyValue_string_value);
                    break;
                case bool b:
                    OtlpProtobufSerializerHelper.WriteBoolWithTag(buffer, ref currentPosition, FieldNumberConstants.AnyValue_bool_value, (bool)value);
                    break;
                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                    OtlpProtobufSerializerHelper.WriteInt64WithTag(buffer, ref currentPosition, FieldNumberConstants.AnyValue_int_value, Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                    break;
                case float:
                case double:
                    OtlpProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref currentPosition, FieldNumberConstants.AnyValue_double_value, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, Convert.ToString(value, CultureInfo.InvariantCulture), FieldNumberConstants.AnyValue_string_value);
                    break;

                    // TODO: Handle array type.
            }

            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousPosition - currentPosition, FieldNumberConstants.KeyValue_value, WireFormat.WireType.LengthDelimited);
            OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, key, FieldNumberConstants.KeyValue_key);
            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref currentPosition, previousPosition - currentPosition, fieldNumber, WireFormat.WireType.LengthDelimited);
            previousPosition = currentPosition;
        }
        catch
        {
            // TODO: log exception.
        }
    }
}

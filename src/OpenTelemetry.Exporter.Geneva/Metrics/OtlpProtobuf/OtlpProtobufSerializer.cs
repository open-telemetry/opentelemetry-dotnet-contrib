// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class OtlpProtobufSerializer
{
    private const int TagAndLengthSize = 4;

    private readonly Dictionary<string, List<Metric>> scopeMetrics = new();

    private int resourceMetricStartIndex;

    private int scopeMetricsStartIndex;

    private int metricStartIndex;

    private int intrumentStartIndex;

    private int metricPointStartIndex;

    internal IMetricDataTransport MetricDataTransport;

    public OtlpProtobufSerializer(IMetricDataTransport metricDataTransport)
    {
        this.MetricDataTransport = metricDataTransport;
    }

    internal void SerializeAndSendMetrics(byte[] buffer, Resource resource, in Batch<Metric> metricBatch)
    {
        foreach (var metric in metricBatch)
        {
            if (this.scopeMetrics.TryGetValue(metric.MeterName, out var metricList))
            {
                metricList.Add(metric);
            }
            else
            {
                var newList = new List<Metric>() { metric };
                this.scopeMetrics[metric.MeterName] = newList;
            }
        }

        // Serialize
        this.SerializeResourceMetrics(buffer, resource);

        this.ClearScopeMetrics();
    }

    internal void ClearScopeMetrics()
    {
        foreach (var entry in this.scopeMetrics)
        {
            entry.Value.Clear();
        }
    }

    internal void SerializeResourceMetrics(byte[] buffer, Resource resource)
    {
        int cursor = 0;

        this.resourceMetricStartIndex = cursor;

        cursor += TagAndLengthSize;

        // Serialize Resource
        // TODO: Avoid serializing it multiple times.
        SerializeResource(buffer, ref cursor, resource);

        // TODO: Serialize schema_url field

        // Serialize ScopeMetrics field
        this.scopeMetricsStartIndex = cursor;
        foreach (KeyValuePair<string, List<Metric>> entry in this.scopeMetrics)
        {
            if (entry.Value.Count > 0)
            {
                cursor = this.scopeMetricsStartIndex;

                cursor += TagAndLengthSize;

                // Serialize this meter/scope
                this.SerializeScopeMetrics(buffer, ref cursor, entry.Key, entry.Value);
            }
        }
    }

    internal void SerializeScopeMetrics(byte[] buffer, ref int cursor, string scopeName, List<Metric> metrics)
    {
        // Serialize scope information
        // TODO: Avoid serializing for each export.
        SerializeInstrumentationScope(buffer, ref cursor, scopeName, metrics[0].MeterTags);

        this.metricStartIndex = cursor;
        foreach (Metric metric in metrics)
        {
            cursor = this.metricStartIndex;

            cursor += TagAndLengthSize;

            // Serialize metrics for the meter/scope
            this.SerializeMetric(buffer, ref cursor, metric);
        }

        // TODO: Serialize schema_url field.
    }

    private void SerializeMetric(byte[] buffer, ref int cursor, Metric metric)
    {
        WriteInstrumentDetails(buffer, ref cursor, metric);

        this.intrumentStartIndex = cursor;
        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    cursor = this.intrumentStartIndex;

                    cursor += TagAndLengthSize;

                    // Write isMonotonic tag
                    ProtobufSerializerHelper.WriteBoolWithTag(buffer, ref cursor, FieldNumberConstants.Sum_is_monotonic, metric.MetricType == MetricType.LongSum);

                    // Write aggregationTemporality tag
                    ProtobufSerializerHelper.WriteEnumWithTag(buffer, ref cursor, FieldNumberConstants.Sum_aggregation_temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

                    this.metricPointStartIndex = cursor;
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        cursor = this.metricPointStartIndex;

                        cursor += TagAndLengthSize;

                        var sum = (ulong)metricPoint.GetSumLong();

                        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.NumberDataPoint_as_int, sum);

                        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.NumberDataPoint_start_time_unix_nano, startTime);

                        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.NumberDataPoint_time_unix_nano, endTime);

                        SerializeTags(buffer, ref cursor, metricPoint.Tags, FieldNumberConstants.NumberDataPoint_attributes);

                        // TODO: exemplars.

                        var metricPointStartPosition = this.metricPointStartIndex;

                        // Write numberdatapoint {Repeated field}
                        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref metricPointStartPosition, cursor - metricPointStartPosition - TagAndLengthSize, FieldNumberConstants.Sum_data_points, WireFormat.WireType.LengthDelimited);

                        // Finish writing current batch
                        this.WriteIndividualMessageTagsAndLength(buffer, ref cursor, metric.MetricType);

                        // Send metricPoint
                        this.SendMetricPoint(buffer, ref cursor);
                    }

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
    }

    private void WriteIndividualMessageTagsAndLength(byte[] buffer, ref int cursor, MetricType metricType)
    {
        var instrumentIndex = this.intrumentStartIndex;

        var metricIndex = this.metricStartIndex;

        var scopeMetricsIndex = this.scopeMetricsStartIndex;

        var resourceMetricIndex = this.resourceMetricStartIndex;

        // Write instrument tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref instrumentIndex, cursor - instrumentIndex - TagAndLengthSize, FieldNumberConstants.GetMetricTypeFieldNumber(metricType), WireFormat.WireType.LengthDelimited);

        // Write metric tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref metricIndex, cursor - metricIndex - TagAndLengthSize, FieldNumberConstants.ScopeMetrics_metrics, WireFormat.WireType.LengthDelimited);

        // Write scope tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref scopeMetricsIndex, cursor - scopeMetricsIndex - TagAndLengthSize, FieldNumberConstants.ResourceMetrics_scope_metrics, WireFormat.WireType.LengthDelimited);

        // Write resource metric tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref resourceMetricIndex, cursor - resourceMetricIndex - TagAndLengthSize, FieldNumberConstants.ResourceMetrics_resource, WireFormat.WireType.LengthDelimited);
    }

    private void SendMetricPoint(byte[] buffer, ref int cursor)
    {
        // TODO: Extend this for user_events.
        this.MetricDataTransport.SendOtlpProtobufEvent(buffer, cursor);
    }

    internal static void WriteInstrumentDetails(byte[] buffer, ref int cursor, Metric metric)
    {
        // Write metric name
        ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.Metric_name, metric.Name);

        // Write metric description
        if (metric.Description != null)
        {
            ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.Metric_description, metric.Description);
        }

        // Write metric unit
        if (metric.Unit != null)
        {
            ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.Metric_unit, metric.Unit);
        }
    }

    internal static void SerializeInstrumentationScope(byte[] buffer, ref int cursor, string name, IEnumerable<KeyValuePair<string, object>> meterTags)
    {
        int previousPosition = cursor;

        cursor += TagAndLengthSize;

        // Write name
        ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.InstrumentationScope_name, name);

        SerializeTags(buffer, ref cursor, meterTags, FieldNumberConstants.InstrumentationScope_attributes);

        // Write instrumentation Scope Tag
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref previousPosition, cursor - previousPosition - TagAndLengthSize, FieldNumberConstants.ScopeMetrics_scope, WireFormat.WireType.LengthDelimited);
    }

    private static void SerializeTags(byte[] buffer, ref int cursor, IEnumerable<KeyValuePair<string, object>> attributes, int fieldNumber)
    {
        if (attributes != null)
        {
            foreach (var tag in attributes)
            {
                if (tag.Value != null)
                {
                    SerializeTag(buffer, ref cursor, tag.Key, tag.Value, fieldNumber);
                }
            }
        }
    }

    private static void SerializeResource(byte[] buffer, ref int cursor, Resource resource)
    {
        if (resource != null && resource != Resource.Empty)
        {
            cursor += TagAndLengthSize;

            var previousPosition = cursor;
            SerializeTags(buffer, ref cursor, resource.Attributes, FieldNumberConstants.Resource_attributes);
            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref previousPosition, cursor - previousPosition - TagAndLengthSize, FieldNumberConstants.ResourceMetrics_resource, WireFormat.WireType.LengthDelimited);
        }
    }

    internal static void SerializeTags(byte[] buffer, ref int cursor, ReadOnlyTagCollection tags, int fieldNumber)
    {
        foreach (var tag in tags)
        {
            if (tag.Value != null)
            {
                SerializeTag(buffer, ref cursor, tag.Key, tag.Value, fieldNumber);
            }
        }
    }

    internal static void SerializeTag(byte[] buffer, ref int cursor, string key, object value, int fieldNumber)
    {
        try
        {
            // TODO : Check if calculating the length in advance could be more efficient in this case.
            // That way we wouldn't have to leave the fixed length space.
            int keyValueLengthPosition = cursor;
            cursor += TagAndLengthSize;

            ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.KeyValue_key, key);

            int valuePosition = cursor;
            cursor += TagAndLengthSize;

            switch (value)
            {
                case char:
                case string:
                    ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.AnyValue_string_value, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case bool b:
                    ProtobufSerializerHelper.WriteBoolWithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_bool_value, (bool)value);
                    break;
                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                    ProtobufSerializerHelper.WriteInt64WithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_int_value, (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case float:
                case double:
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_double_value, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.AnyValue_string_value, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;

                    // TODO: Handle array type.
            }

            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref valuePosition, cursor - valuePosition - TagAndLengthSize, FieldNumberConstants.KeyValue_value, WireFormat.WireType.LengthDelimited);
            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref keyValueLengthPosition, cursor - keyValueLengthPosition - TagAndLengthSize, fieldNumber, WireFormat.WireType.LengthDelimited);
        }
        catch
        {
            // TODO: log exception.
        }
    }
}

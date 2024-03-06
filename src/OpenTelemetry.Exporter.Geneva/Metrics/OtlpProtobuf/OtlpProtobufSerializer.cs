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
    private const int LengthAndTagSize = 4;

    private static readonly ConcurrentBag<List<Metric>> MetricListPool = new();

    private readonly Dictionary<string, List<Metric>> scopeMetrics = new();

    private int resourceMetricStartIndex;

    private int previousScopeMetricsStartIndex;

    private int previousMetricStartIndex;

    private int previousIntrumentStartIndex;

    private int previousMetricPointStartIndex;

    internal IMetricDataTransport MetricDataTransport;

    public OtlpProtobufSerializer()
    {
        this.MetricDataTransport = MetricEtwDataTransport.Instance;
    }

    internal void SerializeAndSendMetrics(byte[] buffer, ref int currentPosition, Resource resource, in Batch<Metric> metricBatch)
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
        this.SerializeResourceMetrics(buffer, ref currentPosition, resource);

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

    internal void SerializeResourceMetrics(byte[] buffer, ref int currentPosition, Resource resource)
    {
        this.resourceMetricStartIndex = currentPosition;

        currentPosition += LengthAndTagSize;

        // Serialize Resource
        // TODO: Avoid serializing it multiple times.
        SerializeResource(buffer, ref currentPosition, resource);

        // TODO: Serialize schema_url field

        // Serialize ScopeMetrics field
        foreach (KeyValuePair<string, List<Metric>> metric in this.scopeMetrics)
        {
            this.previousScopeMetricsStartIndex = currentPosition;

            currentPosition += LengthAndTagSize;

            // Serialize this meter/scope
            this.SerializeScopeMetrics(buffer, ref currentPosition, metric.Key, metric.Value);

            // Reset the current position to start writing again from where we started the last scope.
            currentPosition = this.previousScopeMetricsStartIndex;
        }
    }

    internal void SerializeScopeMetrics(byte[] buffer, ref int currentPosition, string scopeName, List<Metric> metrics)
    {
        bool scopeInformationIncluded = false;

        foreach (Metric metric in metrics)
        {
            if (!scopeInformationIncluded)
            {
                // Serialize scope information
                SerializeInstrumentationScope(buffer, ref currentPosition, scopeName, metric.MeterTags);

                scopeInformationIncluded = true;
            }

            this.previousMetricStartIndex = currentPosition;

            currentPosition += LengthAndTagSize;

            // Serialize metrics for the meter/scope
            this.SerializeMetric(buffer, ref currentPosition, metric);

            // Reset the current position to start writing again from where we started the last metric.
            currentPosition = this.previousMetricStartIndex;
        }

        // TODO: Serialize schema_url field.
    }

    private void SerializeMetric(byte[] buffer, ref int currentPosition, Metric metric)
    {
        WriteInstrumentDetails(buffer, ref currentPosition, metric);

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    this.previousIntrumentStartIndex = currentPosition;

                    currentPosition += LengthAndTagSize;

                    // Write isMonotonic tag
                    OtlpProtobufSerializerHelper.WriteBoolWithTag(buffer, ref currentPosition, FieldNumberConstants.Sum_is_monotonic, metric.MetricType == MetricType.LongSum);

                    // Write aggregationTemporality tag
                    OtlpProtobufSerializerHelper.WriteEnumWithTag(buffer, ref currentPosition, FieldNumberConstants.Sum_aggregation_temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        this.previousMetricPointStartIndex = currentPosition;

                        currentPosition += LengthAndTagSize;

                        var sum = (ulong)metricPoint.GetSumLong();

                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_as_int, sum);

                        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_start_time_unix_nano, startTime);

                        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                        OtlpProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref currentPosition, FieldNumberConstants.NumberDataPoint_time_unix_nano, endTime);

                        SerializeTags(buffer, ref currentPosition, metricPoint.Tags, FieldNumberConstants.NumberDataPoint_attributes);

                        // TODO: exemplars.

                        var previousMetricPointPosition = this.previousMetricPointStartIndex;

                        // Write numberdatapoint {Repeated field}
                        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref previousMetricPointPosition, currentPosition - previousMetricPointPosition - LengthAndTagSize, FieldNumberConstants.Sum_data_points, WireFormat.WireType.LengthDelimited);

                        // Finish writing current batch
                        this.WriteIndividualMessageTagsAndLength(buffer, ref currentPosition, metric.MetricType);

                        // Send metricPoint
                        this.SendMetricPointAndResetCurrentBufferPosition(buffer, ref currentPosition);
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

    private void WriteIndividualMessageTagsAndLength(byte[] buffer, ref int currentPosition, MetricType metricType)
    {
        var instrumentIndex = this.previousIntrumentStartIndex;

        var metricIndex = this.previousMetricStartIndex;

        var scopeMetricsIndex = this.previousScopeMetricsStartIndex;

        var resourceMetricIndex = this.resourceMetricStartIndex;

        // Write instrument tag and length
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref instrumentIndex, currentPosition - instrumentIndex - LengthAndTagSize, FieldNumberConstants.GetMetricTypeFieldNumber(metricType), WireFormat.WireType.LengthDelimited);

        // Write metric tag and length
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref metricIndex, currentPosition - metricIndex - LengthAndTagSize, FieldNumberConstants.ScopeMetrics_metrics, WireFormat.WireType.LengthDelimited);

        // Write scope tag and length
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref scopeMetricsIndex, currentPosition - scopeMetricsIndex - LengthAndTagSize, FieldNumberConstants.ResourceMetrics_scope_metrics, WireFormat.WireType.LengthDelimited);

        // Write resource metric tag and length
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref resourceMetricIndex, currentPosition - resourceMetricIndex - LengthAndTagSize, FieldNumberConstants.ResourceMetrics_resource, WireFormat.WireType.LengthDelimited);
    }

    private void SendMetricPointAndResetCurrentBufferPosition(byte[] buffer, ref int currentPosition)
    {
        // TODO: Extend this for user_events.
        this.MetricDataTransport.SendOtlpProtobufEvent(buffer, currentPosition);

        currentPosition = this.previousMetricPointStartIndex;
    }

    internal static void WriteInstrumentDetails(byte[] buffer, ref int currentPosition, Metric metric)
    {
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

        currentPosition += LengthAndTagSize;

        // Write name
        OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, name, 1);

        SerializeTags(buffer, ref currentPosition, meterTags, FieldNumberConstants.InstrumentationScope_attributes);

        // Write instrumentation Scope Tag
        OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref previousPosition, currentPosition - previousPosition - LengthAndTagSize, FieldNumberConstants.ScopeMetrics_scope, WireFormat.WireType.LengthDelimited);
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

    private static void SerializeResource(byte[] buffer, ref int currentPosition, Resource resource)
    {
        if (resource != null && resource.Attributes != null)
        {
            var previousPosition = currentPosition;
            currentPosition += LengthAndTagSize;
            SerializeTags(buffer, ref currentPosition, resource.Attributes, FieldNumberConstants.Resource_attributes);
            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref previousPosition, currentPosition - previousPosition - LengthAndTagSize, FieldNumberConstants.ResourceMetrics_resource, WireFormat.WireType.LengthDelimited);
        }
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
        try
        {
            // TODO : Check if calculating the length in advance could be more efficient in this case.
            // That way we wouldn't have to leave the fixed length space.
            int keyValueLengthPosition = currentPosition;
            currentPosition += LengthAndTagSize;

            OtlpProtobufSerializerHelper.WriteStringTag(buffer, ref currentPosition, key, FieldNumberConstants.KeyValue_key);

            int valuePosition = currentPosition;
            currentPosition += LengthAndTagSize;

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

            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref valuePosition, currentPosition - valuePosition - LengthAndTagSize, FieldNumberConstants.KeyValue_value, WireFormat.WireType.LengthDelimited);
            OtlpProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref keyValueLengthPosition, currentPosition - keyValueLengthPosition - LengthAndTagSize, fieldNumber, WireFormat.WireType.LengthDelimited);
        }
        catch
        {
            // TODO: log exception.
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class OtlpProtobufSerializer
{
    internal IMetricDataTransport MetricDataTransport;

    private const int TagAndLengthSize = 4;

    private readonly Dictionary<string, List<Metric>> scopeMetrics = [];
    private readonly byte[] buffer;
    private readonly string? metricNamespace;
    private readonly string? metricAccount;
    private readonly bool prefixBufferWithUInt32LittleEndianLength;
    private readonly byte[]? prepopulatedNumberDataPointAttributes;
    private readonly byte[]? prepopulatedHistogramDataPointAttributes;
    private readonly byte[]? prepopulatedExponentialHistogramDataPointAttributes;
    private readonly int resourceMetricTagAndLengthIndex;
    private int scopeMetricsTagAndLengthIndex;
    private int metricTagAndLengthIndex;
    private int instrumentTagAndLengthIndex;
    private ExportResult metricExportResult;

    public OtlpProtobufSerializer(
        IMetricDataTransport metricDataTransport,
        byte[] buffer,
        string? metricsAccount,
        string? metricsNamespace,
        IReadOnlyDictionary<string, object>? prepopulatedMetricDimensions,
        bool prefixBufferWithUInt32LittleEndianLength = false)
    {
        Debug.Assert(metricDataTransport != null, "metricDataTransport was null");

        this.MetricDataTransport = metricDataTransport!;
        this.buffer = buffer;
        this.metricAccount = metricsAccount;
        this.metricNamespace = metricsNamespace;
        this.prefixBufferWithUInt32LittleEndianLength = prefixBufferWithUInt32LittleEndianLength;
        this.resourceMetricTagAndLengthIndex = prefixBufferWithUInt32LittleEndianLength ? 4 : 0;
        this.scopeMetricsTagAndLengthIndex = this.resourceMetricTagAndLengthIndex + TagAndLengthSize;

        if (prepopulatedMetricDimensions != null)
        {
            // Initialize numberDataPoint attributes.
            var cursor = SerializeTags(buffer, 0, prepopulatedMetricDimensions!, FieldNumberConstants.NumberDataPoint_attributes);
            this.prepopulatedNumberDataPointAttributes = buffer.AsSpan(0, cursor).ToArray();

            // Initialize histogramDataPoint attributes.
            cursor = SerializeTags(buffer, 0, prepopulatedMetricDimensions!, FieldNumberConstants.HistogramDataPoint_attributes);
            this.prepopulatedHistogramDataPointAttributes = buffer.AsSpan(0, cursor).ToArray();

            cursor = SerializeTags(buffer, 0, prepopulatedMetricDimensions!, FieldNumberConstants.ExponentialHistogramDataPoint_attributes);
            this.prepopulatedExponentialHistogramDataPointAttributes = buffer.AsSpan(0, cursor).ToArray();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    internal static void SerializeInstrumentationScope(byte[] buffer, ref int cursor, string name, IEnumerable<KeyValuePair<string, object?>>? meterTags)
    {
        var tagAndLengthIndex = cursor;
        cursor += TagAndLengthSize;

        // Write name
        ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.InstrumentationScope_name, name);

        if (meterTags != null)
        {
            cursor = SerializeTags(buffer, cursor, meterTags, FieldNumberConstants.InstrumentationScope_attributes);
        }

        // Write instrumentation Scope Tag
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, tagAndLengthIndex, cursor, FieldNumberConstants.ScopeMetrics_scope, WireType.LEN);
    }

    internal static int SerializeTags(byte[] buffer, int cursor, ReadOnlyTagCollection tags, int fieldNumber)
    {
        foreach (var tag in tags)
        {
            if (tag.Value != null)
            {
                SerializeTag(buffer, ref cursor, tag.Key, tag.Value, fieldNumber);
            }
        }

        return cursor;
    }

    internal static void SerializeTag(byte[] buffer, ref int cursorValue, string key, object value, int fieldNumber)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            var cursor = cursorValue;
            var keyValueTagAndLengthIndex = cursor;
            cursor += TagAndLengthSize;

            ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.KeyValue_key, key);

            var anyValueTagAndLengthIndex = cursor;
            cursor += TagAndLengthSize;

            long intValue;
            switch (value)
            {
                case string s:
                    string stringValue = s;
writeString:
                    ProtobufSerializerHelper.WriteStringTag(buffer, ref cursor, FieldNumberConstants.AnyValue_string_value, stringValue);
                    break;
                case bool b:
                    ProtobufSerializerHelper.WriteBoolWithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_bool_value, b);
                    break;
                case byte u8: intValue = u8; goto writeInt64;
                case sbyte i8: intValue = i8; goto writeInt64;
                case short i16: intValue = i16; goto writeInt64;
                case ushort u16: intValue = u16; goto writeInt64;
                case int i32: intValue = i32; goto writeInt64;
                case uint u32: intValue = u32; goto writeInt64;
                case long i64: intValue = i64; goto writeInt64;
                case ulong u64:
                    intValue = (long)u64;
writeInt64:
                    ProtobufSerializerHelper.WriteInt64WithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_int_value, (ulong)intValue);
                    break;
                case double:
                    double doubleValue = (double)value;
writeDouble:
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.AnyValue_double_value, doubleValue);
                    break;
                case float: doubleValue = (float)value; goto writeDouble;
                case char c: stringValue = c.ToString(); goto writeString;
                default: stringValue = ConvertToString(value); goto writeString;

                    // TODO: Handle array type.
            }

            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, anyValueTagAndLengthIndex, cursor, FieldNumberConstants.KeyValue_value, WireType.LEN);
            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, keyValueTagAndLengthIndex, cursor, fieldNumber, WireType.LEN);
            cursorValue = cursor;
        }
        catch
        {
            // TODO: log exception.
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void InitializeResource(Resource resource)
    {
        var cursor = this.resourceMetricTagAndLengthIndex + TagAndLengthSize;
        this.SerializeResource(this.buffer, ref cursor, resource);
        this.scopeMetricsTagAndLengthIndex = cursor;
    }

    internal ExportResult SerializeAndSendMetrics(in Batch<Metric> metricBatch)
    {
        this.metricExportResult = ExportResult.Success;

        var scopeMetrics = this.scopeMetrics;
        foreach (var metric in metricBatch)
        {
            if (!scopeMetrics.TryGetValue(metric.MeterName, out var metricList))
            {
                scopeMetrics[metric.MeterName] = metricList = [];
            }

            metricList.Add(metric);
        }

        // Serialize
        this.SerializeResourceMetrics();
        return this.metricExportResult;
    }

    internal void SerializeResourceMetrics()
    {
        // Resource has been already serialized to the buffer in InitializeResource

        // TODO: Serialize schema_url field

        // Serialize ScopeMetrics field
        foreach (var entry in this.scopeMetrics)
        {
            if (entry.Value.Count > 0)
            {
                // Serialize this meter/scope
                this.SerializeScopeMetrics(entry.Key, entry.Value);
                entry.Value.Clear();
            }
        }
    }

    internal void SerializeScopeMetrics(string scopeName, List<Metric> metrics)
    {
        var buffer = this.buffer;
        var cursor = this.scopeMetricsTagAndLengthIndex + TagAndLengthSize;

        // Serialize scope information
        // TODO: Avoid serializing for each export.
        SerializeInstrumentationScope(buffer, ref cursor, scopeName, metrics[0].MeterTags);

        this.metricTagAndLengthIndex = cursor;
        cursor += TagAndLengthSize;
        foreach (var metric in metrics)
        {
            // Serialize metrics for the meter/scope
            this.SerializeMetric(buffer, cursor, metric);
        }

        // TODO: Serialize schema_url field.
    }

    private static int SerializeExemplars<T>(byte[] buffer, int cursor, ReadOnlyExemplarCollection exemplars, int fieldNumber)
    {
        foreach (ref readonly var exemplar in exemplars)
        {
            var exemplarTagAndLengthIndex = cursor;
            cursor += TagAndLengthSize;

            cursor = SerializeExemplarTags(buffer, cursor, exemplar.FilteredTags);

            // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
            // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
            ProtobufSerializerHelper.WriteFixed64WithTag(
                buffer,
                ref cursor,
                typeof(T) == typeof(long) ? FieldNumberConstants.Exemplar_as_int : FieldNumberConstants.Exemplar_as_double,
                (ulong)exemplar.LongValue);

            var time = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();
            ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.Exemplar_time_unix_nano, time);

            if (exemplar.SpanId != default)
            {
                ProtobufSerializerHelper.WriteTag(buffer, ref cursor, FieldNumberConstants.Exemplar_trace_id, WireType.LEN);
                buffer[cursor++] = 16;
                var traceBytes = new Span<byte>(buffer, cursor, 16);
                exemplar.TraceId.CopyTo(traceBytes);
                cursor += 16;

                ProtobufSerializerHelper.WriteTag(buffer, ref cursor, FieldNumberConstants.Exemplar_span_id, WireType.LEN);
                buffer[cursor++] = 8;
                var spanBytes = new Span<byte>(buffer, cursor, 8);
                exemplar.SpanId.CopyTo(spanBytes);
                cursor += 8;
            }

            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, exemplarTagAndLengthIndex, cursor, fieldNumber, WireType.LEN);
        }

        return cursor;
    }

    private static int SerializeExemplarTags(byte[] buffer, int cursor, ReadOnlyFilteredTagCollection tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Value != null)
            {
                SerializeTag(buffer, ref cursor, tag.Key, tag.Value, FieldNumberConstants.Exemplar_attributes);
            }
        }

        return cursor;
    }

    private static int SerializeTags(byte[] buffer, int cursor, IEnumerable<KeyValuePair<string, object?>> attributes, int fieldNumber)
    {
        foreach (var tag in attributes)
        {
            if (tag.Value != null)
            {
                SerializeTag(buffer, ref cursor, tag.Key, tag.Value, fieldNumber);
            }
        }

        return cursor;
    }

    private static string ConvertToString(object value)
    {
        try
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch
        {
            return $"ERROR: type {value.GetType().FullName} is not supported";
        }
    }

    private void SerializeMetric(byte[] buffer, int cursor, Metric metric)
    {
        WriteInstrumentDetails(buffer, ref cursor, metric);

        this.instrumentTagAndLengthIndex = cursor;
        cursor += TagAndLengthSize;
        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                this.SerializeNonHistogram<DpSumLong>(buffer, cursor, metric);
                break;
            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                this.SerializeNonHistogram<DpSumDouble>(buffer, cursor, metric);
                break;
            case MetricType.LongGauge:
                this.SerializeNonHistogram<DpGaugeLong>(buffer, cursor, metric);
                break;
            case MetricType.DoubleGauge:
                this.SerializeNonHistogram<DpGaugeDouble>(buffer, cursor, metric);
                break;
            case MetricType.Histogram:
                this.SerializeHistogramMetric(buffer, cursor, metric);
                break;
            case MetricType.ExponentialHistogram:
                this.SerializeExponentialHistogramMetric(buffer, cursor, metric);
                break;
        }
    }

    private void SerializeNonHistogram<T>(byte[] buffer, int metricPointIndex, Metric metric)
    {
        if (typeof(T) == typeof(DpSumLong) || typeof(T) == typeof(DpSumDouble))
        {
            // Write isMonotonic tag
            ProtobufSerializerHelper.WriteBoolWithTag(buffer, ref metricPointIndex, FieldNumberConstants.Sum_is_monotonic, metric.MetricType <= MetricType.DoubleSum);

            // Write aggregationTemporality tag
            ProtobufSerializerHelper.WriteEnumWithTag(buffer, ref metricPointIndex, FieldNumberConstants.Sum_aggregation_temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);
        }

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            try
            {
                // Reset cursor to write new metricPoint
                var cursor = metricPointIndex + TagAndLengthSize;
                cursor = this.WriteNumberDataPoint<T>(buffer, cursor, in metricPoint);

                // Finish writing current batch
                var fieldNumber = typeof(T) == typeof(DpSumLong) || typeof(T) == typeof(DpSumDouble) ? FieldNumberConstants.Sum_data_points : FieldNumberConstants.Gauge_data_points;
                this.WriteIndividualMessageTagsAndLength(buffer, cursor, metricPointIndex, fieldNumber, metric.MetricType);

                // Send metricPoint
                this.SendMetricPoint(buffer, cursor);
            }
            catch (Exception ex)
            {
                this.metricExportResult = ExportResult.Failure;
                ExporterEventSource.Log.FailedToSerializeMetric(metric.Name, ex);
            }
        }
    }

    private void SerializeHistogramMetric(byte[] buffer, int metricPointIndex, Metric metric)
    {
        // Write aggregationTemporality tag
        ProtobufSerializerHelper.WriteEnumWithTag(buffer, ref metricPointIndex, FieldNumberConstants.Histogram_aggregation_temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            try
            {
                // Reset cursor to write new metricPoint
                var cursor = metricPointIndex + TagAndLengthSize;

                var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_start_time_unix_nano, startTime);

                var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_time_unix_nano, endTime);

                cursor = SerializeTags(buffer, cursor, metricPoint.Tags, FieldNumberConstants.HistogramDataPoint_attributes);

                if (this.prepopulatedHistogramDataPointAttributes is { } prepopulated)
                {
                    Array.Copy(prepopulated, 0, buffer, cursor, prepopulated.Length);
                    cursor += prepopulated.Length;
                }

                var count = (ulong)metricPoint.GetHistogramCount();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_count, count);

                ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_sum, metricPoint.GetHistogramSum());

                if (metricPoint.TryGetHistogramMinMaxValues(out var min, out var max))
                {
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_min, min);
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_max, max);
                }

                foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                {
                    var bucketCount = (ulong)histogramMeasurement.BucketCount;

                    ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_bucket_counts, bucketCount);
                    if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                    {
                        ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.HistogramDataPoint_explicit_bounds, histogramMeasurement.ExplicitBound);
                    }
                }

                if (metricPoint.TryGetExemplars(out var exemplars))
                {
                    cursor = SerializeExemplars<double>(buffer, cursor, exemplars, FieldNumberConstants.HistogramDataPoint_exemplars);
                }

                // Finish writing current batch
                this.WriteIndividualMessageTagsAndLength(buffer, cursor, metricPointIndex, FieldNumberConstants.Histogram_data_points, MetricType.Histogram);

                // Send metricPoint
                this.SendMetricPoint(buffer, cursor);
            }
            catch (Exception ex)
            {
                this.metricExportResult = ExportResult.Failure;
                ExporterEventSource.Log.FailedToSerializeMetric(metric.Name, ex);
            }
        }
    }

    private void SerializeExponentialHistogramMetric(byte[] buffer, int metricPointIndex, Metric metric)
    {
        // Write aggregationTemporality tag
        ProtobufSerializerHelper.WriteEnumWithTag(buffer, ref metricPointIndex, FieldNumberConstants.ExponentialHistogram_aggregation_temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            try
            {
                // Reset cursor to write new metricPoint
                var cursor = metricPointIndex + TagAndLengthSize;

                var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_start_time_unix_nano, startTime);

                var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_time_unix_nano, endTime);

                cursor = SerializeTags(buffer, cursor, metricPoint.Tags, FieldNumberConstants.ExponentialHistogramDataPoint_attributes);

                if (this.prepopulatedExponentialHistogramDataPointAttributes is { } prepopulated)
                {
                    Array.Copy(prepopulated, 0, buffer, cursor, prepopulated.Length);
                    cursor += prepopulated.Length;
                }

                var sum = metricPoint.GetHistogramSum();

                ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_sum, sum);

                var count = (ulong)metricPoint.GetHistogramCount();
                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_count, count);

                if (metricPoint.TryGetHistogramMinMaxValues(out var min, out var max))
                {
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_min, min);
                    ProtobufSerializerHelper.WriteDoubleWithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_max, max);
                }

                var exponentialHistogramData = metricPoint.GetExponentialHistogramData();

                ProtobufSerializerHelper.WriteSInt32WithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_scale, exponentialHistogramData.Scale);

                ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.ExponentialHistogramDataPoint_zero_count, (ulong)exponentialHistogramData.ZeroCount);

                var positiveBucketIndex = cursor;
                cursor += TagAndLengthSize;

                ProtobufSerializerHelper.WriteSInt32WithTag(buffer, ref cursor, FieldNumberConstants.Bucket_offset, exponentialHistogramData.PositiveBuckets.Offset);

                foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                {
                    ProtobufSerializerHelper.WriteInt64WithTag(buffer, ref cursor, FieldNumberConstants.Bucket_bucket_counts, (ulong)bucketCount);
                }

                // write Bucket
                ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, positiveBucketIndex, cursor, FieldNumberConstants.ExponentialHistogramDataPoint_positive, WireType.LEN);

                if (metricPoint.TryGetExemplars(out var exemplars))
                {
                    cursor = SerializeExemplars<double>(buffer, cursor, exemplars, FieldNumberConstants.ExponentialHistogramDataPoint_exemplars);
                }

                // Finish writing current batch
                this.WriteIndividualMessageTagsAndLength(buffer, cursor, metricPointIndex, FieldNumberConstants.ExponentialHistogram_data_points, MetricType.ExponentialHistogram);

                // Send metricPoint
                this.SendMetricPoint(buffer, cursor);
            }
            catch (Exception ex)
            {
                this.metricExportResult = ExportResult.Failure;
                ExporterEventSource.Log.FailedToSerializeMetric(metric.Name, ex);
            }
        }
    }

    private int WriteNumberDataPoint<T>(byte[] buffer, int cursor, in MetricPoint metricPoint)
    {
        int valueTag;
        long value;
        if (typeof(T) == typeof(DpSumLong))
        {
            valueTag = FieldNumberConstants.NumberDataPoint_as_int;
            value = metricPoint.GetSumLong();
        }
        else if (typeof(T) == typeof(DpSumDouble))
        {
            valueTag = FieldNumberConstants.NumberDataPoint_as_double;
            value = BitConverter.DoubleToInt64Bits(metricPoint.GetSumDouble());
        }
        else if (typeof(T) == typeof(DpGaugeLong))
        {
            valueTag = FieldNumberConstants.NumberDataPoint_as_int;
            value = metricPoint.GetGaugeLastValueLong();
        }
        else
        {
            valueTag = FieldNumberConstants.NumberDataPoint_as_double;
            value = BitConverter.DoubleToInt64Bits(metricPoint.GetGaugeLastValueDouble());
        }

        // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
        // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, valueTag, (ulong)value);

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.NumberDataPoint_start_time_unix_nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        ProtobufSerializerHelper.WriteFixed64WithTag(buffer, ref cursor, FieldNumberConstants.NumberDataPoint_time_unix_nano, endTime);

        cursor = SerializeTags(buffer, cursor, metricPoint.Tags, FieldNumberConstants.NumberDataPoint_attributes);

        if (this.prepopulatedNumberDataPointAttributes is { } prepopulated)
        {
            Array.Copy(prepopulated, 0, buffer, cursor, prepopulated.Length);
            cursor += prepopulated.Length;
        }

        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            cursor = typeof(T) == typeof(DpSumLong) || typeof(T) == typeof(DpGaugeLong)
                ? SerializeExemplars<long>(buffer, cursor, exemplars, FieldNumberConstants.NumberDataPoint_exemplars)
                : SerializeExemplars<double>(buffer, cursor, exemplars, FieldNumberConstants.NumberDataPoint_exemplars);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIndividualMessageTagsAndLength(byte[] buffer, int cursor, int metricPointIndex, int fieldNumber, MetricType metricType)
    {
        // Write metric points tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, metricPointIndex, cursor, fieldNumber, WireType.LEN);

        // Write instrument tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, this.instrumentTagAndLengthIndex, cursor, FieldNumberConstants.GetMetricTypeFieldNumber(metricType), WireType.LEN);

        // Write metric tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, this.metricTagAndLengthIndex, cursor, FieldNumberConstants.ScopeMetrics_metrics, WireType.LEN);

        // Write scope tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, this.scopeMetricsTagAndLengthIndex, cursor, FieldNumberConstants.ResourceMetrics_scope_metrics, WireType.LEN);

        // Write resource metric tag and length
        ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, this.resourceMetricTagAndLengthIndex, cursor, FieldNumberConstants.ResourceMetrics_resource, WireType.LEN);

        if (this.prefixBufferWithUInt32LittleEndianLength)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)cursor - 4);
        }
    }

    private void SendMetricPoint(byte[] buffer, int cursor)
    {
        this.MetricDataTransport.SendOtlpProtobufEvent(buffer, cursor);
    }

    private void SerializeResource(byte[] buffer, ref int cursor, Resource resource)
    {
        if (resource != Resource.Empty)
        {
            var tagAndLengthIndex = cursor;
            cursor += TagAndLengthSize;

            cursor = SerializeTags(buffer, cursor, resource.Attributes!, FieldNumberConstants.Resource_attributes);

            // TODO: check to see if should de-dupe in case the values are also provided via resource attributes.
            if (this.metricAccount != null)
            {
                SerializeTag(buffer, ref cursor, GenevaMetricExporter.DimensionKeyForCustomMonitoringAccount, this.metricAccount, FieldNumberConstants.Resource_attributes);
            }

            if (this.metricNamespace != null)
            {
                SerializeTag(buffer, ref cursor, GenevaMetricExporter.DimensionKeyForCustomMetricsNamespace, this.metricNamespace, FieldNumberConstants.Resource_attributes);
            }

            ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, tagAndLengthIndex, cursor, FieldNumberConstants.ResourceMetrics_resource, WireType.LEN);
        }
    }

    private struct DpSumLong;

    private struct DpSumDouble;

    private struct DpGaugeLong;

    private struct DpGaugeDouble;
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva.Metrics;

internal sealed class TlvMetricExporter : IDisposable
{
    private readonly int prepopulatedDimensionsCount;
    private readonly IMetricDataTransport metricDataTransport;
    private readonly byte[]? serializedPrepopulatedDimensionsKeys;
    private readonly byte[]? serializedPrepopulatedDimensionsValues;
    private readonly byte[] buffer = new byte[GenevaMetricExporter.BufferSize];
    private readonly string defaultMonitoringAccount;
    private readonly string defaultMetricNamespace;

    private bool isDisposed;

    internal TlvMetricExporter(
        ConnectionStringBuilder connectionStringBuilder,
        IReadOnlyDictionary<string, object>? prepopulatedMetricDimensions)
    {
        this.defaultMonitoringAccount = connectionStringBuilder.Account;
        this.defaultMetricNamespace = connectionStringBuilder.Namespace;

        if (prepopulatedMetricDimensions != null)
        {
            this.prepopulatedDimensionsCount = prepopulatedMetricDimensions.Count;
            this.serializedPrepopulatedDimensionsKeys = this.SerializePrepopulatedDimensionsKeys(prepopulatedMetricDimensions.Keys);
            this.serializedPrepopulatedDimensionsValues = this.SerializePrepopulatedDimensionsValues(prepopulatedMetricDimensions.Values);
        }

        switch (connectionStringBuilder.Protocol)
        {
            case TransportProtocol.Unix:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("Unix domain socket should not be used on Windows.");
                }

                var unixDomainSocketPath = connectionStringBuilder.ParseUnixDomainSocketPath();
                this.metricDataTransport = new MetricUnixDomainSocketDataTransport(unixDomainSocketPath);
                break;
            case TransportProtocol.Unspecified:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    this.metricDataTransport = MetricWindowsEventTracingDataTransport.Instance;
                    break;
                }
                else
                {
                    throw new ArgumentException("Endpoint not specified");
                }

            case TransportProtocol.Etw:
            case TransportProtocol.Tcp:
            case TransportProtocol.Udp:
            case TransportProtocol.EtwTld:
            default:
                throw new NotSupportedException($"Protocol '{connectionStringBuilder.Protocol}' is not supported");
        }
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        try
        {
            // The ETW data transport singleton on Windows should NOT be disposed.
            // On Linux, Unix Domain Socket is used and should be disposed.
            if (this.metricDataTransport != MetricWindowsEventTracingDataTransport.Instance)
            {
                this.metricDataTransport.Dispose();
            }

            this.isDisposed = true;
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("TlvMetricExporter Dispose failed.", ex);
        }
    }

    internal ExportResult Export(in Batch<Metric> batch)
    {
        var monitoringAccount = this.defaultMonitoringAccount;
        var metricNamespace = this.defaultMetricNamespace;

        var result = ExportResult.Success;
        foreach (var metric in batch)
        {
            if (metric.MetricType is not (MetricType.LongSum or MetricType.LongSumNonMonotonic or MetricType.LongGauge
                    or MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic or MetricType.DoubleGauge or MetricType.Histogram))
            {
                continue;
            }

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                try
                {
                    var buffer = this.buffer;
                    var size = this.SerializeMetric(metric, metricPoint, buffer, out monitoringAccount, out metricNamespace);
                    this.metricDataTransport.Send(MetricEventType.TLV, buffer, size);
                }
                catch (Exception ex)
                {
                    ExporterEventSource.Log.FailedToSendMetricData(monitoringAccount, metricNamespace, metric.Name, ex); // TODO: preallocate exception or no exception
                    result = ExportResult.Failure;
                }
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int SerializeMetric(Metric metric, in MetricPoint metricPoint, byte[] buffer, out string monitoringAccount, out string metricNamespace)
    {
        // Leave enough space for the header
        var bufferIndex = Unsafe.SizeOf<BinaryHeader>();
        SerializeMetricName(metric.Name, buffer, ref bufferIndex);

        ulong metricData;
        PayloadType payloadType;
        switch (metric.MetricType)
        {
            case MetricType.LongSum:
                metricData = Convert.ToUInt64(metricPoint.GetSumLong());
                payloadType = PayloadType.ULongMetric;
                goto SerializeNonHistogram;

            // The value here could be negative hence we have to use `MetricEventType.DoubleMetric`
            case MetricType.LongGauge:
                // potential for minor precision loss implicitly going from long->double
                // see: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#implicit-numeric-conversions
                metricData = (ulong)BitConverter.DoubleToInt64Bits(Convert.ToDouble(metricPoint.GetGaugeLastValueLong()));
                payloadType = PayloadType.DoubleMetric;
                goto SerializeNonHistogram;

            // The value here could be negative hence we have to use `MetricEventType.DoubleMetric`
            case MetricType.LongSumNonMonotonic:
                // potential for minor precision loss implicitly going from long->double
                // see: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#implicit-numeric-conversions
                metricData = (ulong)BitConverter.DoubleToInt64Bits(Convert.ToDouble(metricPoint.GetSumLong()));
                payloadType = PayloadType.DoubleMetric;
                goto SerializeNonHistogram;

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                metricData = (ulong)BitConverter.DoubleToInt64Bits(metricPoint.GetSumDouble());
                payloadType = PayloadType.DoubleMetric;
                goto SerializeNonHistogram;

            case MetricType.DoubleGauge:
                metricData = (ulong)BitConverter.DoubleToInt64Bits(metricPoint.GetGaugeLastValueDouble());
                payloadType = PayloadType.DoubleMetric;
SerializeNonHistogram:
                SerializeNonHistogramMetricData(payloadType, metricData, metricPoint, buffer, ref bufferIndex);
                break;

            case MetricType.Histogram:
                bufferIndex = SerializeHistogramMetricData(metricPoint, buffer, bufferIndex);
                break;
        }

        // Serializes metric dimensions and also gets the custom account name and metric namespace
        // if specified by adding custom tags: _microsoft_metrics_namespace and _microsoft_metrics_namespace
        this.SerializeDimensionsAndGetCustomAccountNamespace(metricPoint.Tags, buffer, ref bufferIndex, out monitoringAccount, out metricNamespace);

        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            bufferIndex = SerializeExemplars(exemplars, metric.MetricType, buffer, bufferIndex);
        }

        SerializeMonitoringAccount(monitoringAccount, buffer, ref bufferIndex);

        SerializeMetricNamespace(metricNamespace, buffer, ref bufferIndex);

        // Write the final size of the payload
        ref var header = ref Unsafe.As<byte, BinaryHeader>(ref buffer[0]);
        header.EventId = (ushort)MetricEventType.TLV;
        header.BodyLength = (ushort)(bufferIndex -= Unsafe.SizeOf<BinaryHeader>());
        return bufferIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMetricName(string metricName, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.MetricName);
        MetricSerializer.SerializeString(buffer, ref bufferIndex, metricName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMetricNamespace(string metricNamespace, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.Namespace);
        MetricSerializer.SerializeString(buffer, ref bufferIndex, metricNamespace);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMonitoringAccount(string monitoringAccount, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.AccountName);
        MetricSerializer.SerializeString(buffer, ref bufferIndex, monitoringAccount);
    }

    private static int SerializeExemplars(ReadOnlyExemplarCollection exemplars, MetricType metricType, byte[] buffer, int bufferIndex)
    {
        var exemplarsCount = 0;
        foreach (ref readonly var exemplar in exemplars)
        {
            exemplarsCount++;
        }

        if (exemplarsCount > 0)
        {
            MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.Exemplars);

            // Get a placeholder to add the payloadType length
            nuint payloadTypeStartIndex = (uint)bufferIndex;
            bufferIndex += 2;

            MetricSerializer.SerializeByte(buffer, ref bufferIndex, 0); // version

            MetricSerializer.SerializeUInt32AsBase128(buffer, ref bufferIndex, (uint)exemplarsCount);

            foreach (ref readonly var exemplar in exemplars)
            {
                SerializeSingleExemplar(exemplar, metricType, buffer, ref bufferIndex);
            }

            MetricSerializer.SerializeUInt16Length(buffer, payloadTypeStartIndex, bufferIndex - 2 - (int)payloadTypeStartIndex);
        }

        return bufferIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeSingleExemplar(in Exemplar exemplar, MetricType metricType, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, 0); // version

        var bufferIndexForLength = bufferIndex;
        bufferIndex += 2; // reserve 1 byte for length and 1 byte for flags

        var flags = ExemplarFlags.IsTimestampAvailable; // we only serialize exemplars with Timestamp != default

        if (metricType.IsLong())
        {
            flags |= ExemplarFlags.IsMetricValueDoubleStoredAsLong;
            MetricSerializer.SerializeInt64AsBase128(buffer, ref bufferIndex, exemplar.LongValue); // serialize long value
        }
        else
        {
            MetricSerializer.SerializeFloat64(buffer, ref bufferIndex, exemplar.DoubleValue); // serialize double value
        }

        var bufferIndexForNumberOfLabels = bufferIndex;
        bufferIndex++; // reserve 1 byte for the count of labels

        // Convert exemplar timestamp to unix nanoseconds
        var unixNanoSeconds = exemplar.Timestamp.ToUnixTimeNanoseconds();
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, (ulong)unixNanoSeconds); // serialize timestamp

        if (exemplar.TraceId != default)
        {
            exemplar.TraceId.CopyTo(buffer.AsSpan(bufferIndex, 16)); // serialize traceId
            bufferIndex += 16;
            flags |= ExemplarFlags.TraceIdExists;
        }

        if (exemplar.SpanId != default)
        {
            exemplar.SpanId.CopyTo(buffer.AsSpan(bufferIndex, 8)); // serialize spanId
            bufferIndex += 8;
            flags |= ExemplarFlags.SpanIdExists;
        }

        int numberOfLabels = 0;

        foreach (var tag in exemplar.FilteredTags)
        {
            MetricSerializer.SerializeBase128String(buffer, ref bufferIndex, tag.Key);
            MetricSerializer.SerializeBase128String(buffer, ref bufferIndex, Convert.ToString(tag.Value, CultureInfo.InvariantCulture));
            numberOfLabels++;
        }

        buffer[bufferIndexForNumberOfLabels] = (byte)numberOfLabels;

        buffer[bufferIndexForLength + 1] = (byte)flags;

        var exemplarLength = bufferIndex - bufferIndexForLength + 1;
        buffer[bufferIndexForLength] = checked((byte)exemplarLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeNonHistogramMetricData(PayloadType payloadType, ulong value, in MetricPoint metricPoint, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)payloadType);
        MetricSerializer.SerializeUInt16(buffer, ref bufferIndex, 2 * sizeof(ulong)); // payloadType length
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, (ulong)metricPoint.EndTime.UtcDateTime.ToFileTimeUtc()); // timestamp
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, value);
    }

    private static int SerializeHistogramMetricData(in MetricPoint metricPoint, byte[] buffer, int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.ExternallyAggregatedULongDistributionMetric);
        MetricSerializer.SerializeUInt16(buffer, ref bufferIndex, 5 * sizeof(ulong)); // payloadType length
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt32(metricPoint.GetHistogramCount())); // histogram count + padding
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, (ulong)metricPoint.EndTime.UtcDateTime.ToFileTimeUtc()); // timestamp
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(metricPoint.GetHistogramSum())); // histogram sum
        metricPoint.TryGetHistogramMinMaxValues(out var min, out var max);
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(min)); // histogram min
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(max)); // histogram max

        // Serialize histogram buckets as value-count pairs
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.HistogramULongValueCountPairs);

        // Get a placeholder to add the payloadType length
        nuint payloadStartIndex = (uint)(bufferIndex += 2);

        // Get a placeholder to add the number of value-count pairs added
        // with value being the bucket boundary and count being the respective count
        bufferIndex += 2;

        // Bucket values
        int bucketCount = 0;
        double lastExplicitBound = default;
        foreach (var bucket in metricPoint.GetHistogramBuckets())
        {
            if (bucket.BucketCount > 0)
            {
                SerializeHistogramBucketWithTLV(bucket, buffer, ref bufferIndex, lastExplicitBound);
                bucketCount++;
            }

            lastExplicitBound = bucket.ExplicitBound;
        }

        // Write the number of items in distribution emitted and reset back to end.
        MetricSerializer.SerializeUInt16Length(buffer, payloadStartIndex, bucketCount);

        MetricSerializer.SerializeUInt16Length(buffer, payloadStartIndex - 2, bufferIndex - (int)payloadStartIndex);
        return bufferIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeHistogramBucketWithTLV(in HistogramBucket bucket, byte[] buffer, ref int bufferIndex, double lastExplicitBound)
    {
        var value = bucket.ExplicitBound;
        if (value == double.PositiveInfinity)
        {
            // The bucket to catch the overflows is one greater than the last bound provided
            value = lastExplicitBound + 1;
        }

        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(value));
        MetricSerializer.SerializeUInt32(buffer, ref bufferIndex, Convert.ToUInt32(bucket.BucketCount));
    }

    private static string ConvertTagValueToString(object? value)
    {
        try
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch
        {
            return $"ERROR: type {value?.GetType().FullName} is not supported";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeDimensionsAndGetCustomAccountNamespace(in ReadOnlyTagCollection tags, byte[] buffer, ref int bufferIndex, out string monitoringAccount, out string metricNamespace)
    {
        monitoringAccount = this.defaultMonitoringAccount;
        metricNamespace = this.defaultMetricNamespace;

        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.Dimensions);

        // Get a placeholder to add the payloadType length and dimensions count later
        nuint payloadTypeStartIndex = (uint)bufferIndex;
        bufferIndex += 4;

        // Serialize PrepopulatedDimensions keys
        if (this.serializedPrepopulatedDimensionsKeys is { } prepopulatedDimensionsKeys)
        {
            Array.Copy(prepopulatedDimensionsKeys, 0, buffer, bufferIndex, prepopulatedDimensionsKeys.Length);
            bufferIndex += prepopulatedDimensionsKeys.Length;
        }

        var reservedTags = 0;

        // Serialize MetricPoint Dimension keys
        foreach (var tag in tags)
        {
            // TODO: Data Validation if (tag.Key.Length > GenevaMetricExporter.MaxDimensionNameSize)
            if (tag.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMonitoringAccount, StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMetricsNamespace, StringComparison.OrdinalIgnoreCase))
            {
                reservedTags++;
                continue;
            }

            MetricSerializer.SerializeString(buffer, ref bufferIndex, tag.Key);
        }

        var dimensionsWritten = this.prepopulatedDimensionsCount + tags.Count - reservedTags;

        // Serialize PrepopulatedDimensions values
        if (this.serializedPrepopulatedDimensionsValues is { } prepopulatedDimensionsValues)
        {
            Array.Copy(prepopulatedDimensionsValues, 0, buffer, bufferIndex, prepopulatedDimensionsValues.Length);
            bufferIndex += prepopulatedDimensionsValues.Length;
        }

        // Serialize MetricPoint Dimension values
        foreach (var tag in tags)
        {
            if (tag.Value is not string stringValue)
            {
                stringValue = ConvertTagValueToString(tag.Value);
            }
            else if (tag.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMonitoringAccount, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    monitoringAccount = stringValue;
                }

                continue;
            }
            else if (tag.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMetricsNamespace, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    metricNamespace = stringValue;
                }

                continue;
            }

            // TODO: Data Validation: if (dimensionValue?.Length > GenevaMetricExporter.MaxDimensionValueSize)
            MetricSerializer.SerializeString(buffer, ref bufferIndex, stringValue);
        }

        // Backfill the number of dimensions written
        MetricSerializer.SerializeUInt16Length(buffer, payloadTypeStartIndex + 2, dimensionsWritten);
        MetricSerializer.SerializeUInt16Length(buffer, payloadTypeStartIndex, bufferIndex - 2 - (int)payloadTypeStartIndex);
    }

    private byte[] SerializePrepopulatedDimensionsKeys(IEnumerable<string> keys)
    {
        var index = 0;
        foreach (var key in keys)
        {
            MetricSerializer.SerializeString(this.buffer, ref index, key);
        }

        return this.buffer.AsSpan(0, index).ToArray();
    }

    private byte[] SerializePrepopulatedDimensionsValues(IEnumerable<object> values)
    {
        var index = 0;
        foreach (var value in values)
        {
            MetricSerializer.SerializeString(this.buffer, ref index, ConvertTagValueToString(value));
        }

        return this.buffer.AsSpan(0, index).ToArray();
    }
}

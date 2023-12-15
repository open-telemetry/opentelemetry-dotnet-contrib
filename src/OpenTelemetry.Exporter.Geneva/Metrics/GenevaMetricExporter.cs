// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaMetricExporter : BaseExporter<Metric>
{
    private const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    internal const int MaxDimensionNameSize = 256;

    internal const int MaxDimensionValueSize = 1024;

    internal const string DimensionKeyForCustomMonitoringAccount = "_microsoft_metrics_account";

    internal const string DimensionKeyForCustomMetricsNamespace = "_microsoft_metrics_namespace";

    private readonly ushort prepopulatedDimensionsCount;

    private readonly int fixedPayloadStartIndex;

    private readonly IMetricDataTransport metricDataTransport;

    private readonly List<byte[]> serializedPrepopulatedDimensionsKeys;

    private readonly List<byte[]> serializedPrepopulatedDimensionsValues;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly string defaultMonitoringAccount;

    private readonly string defaultMetricNamespace;

    private bool isDisposed;

    public GenevaMetricExporter(GenevaMetricExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        this.defaultMonitoringAccount = connectionStringBuilder.Account;
        this.defaultMetricNamespace = connectionStringBuilder.Namespace;

        if (options.PrepopulatedMetricDimensions != null)
        {
            this.prepopulatedDimensionsCount = (ushort)options.PrepopulatedMetricDimensions.Count;
            this.serializedPrepopulatedDimensionsKeys = this.SerializePrepopulatedDimensionsKeys(options.PrepopulatedMetricDimensions.Keys);
            this.serializedPrepopulatedDimensionsValues = this.SerializePrepopulatedDimensionsValues(options.PrepopulatedMetricDimensions.Values);
        }

        switch (connectionStringBuilder.Protocol)
        {
            case TransportProtocol.Unix:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new ArgumentException("Unix domain socket should not be used on Windows.");
                }

                var unixDomainSocketPath = connectionStringBuilder.ParseUnixDomainSocketPath();
                this.metricDataTransport = new MetricUnixDataTransport(unixDomainSocketPath);
                break;
            case TransportProtocol.Unspecified:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    this.metricDataTransport = MetricEtwDataTransport.Shared;
                    break;
                }
                else
                {
                    throw new ArgumentException("Endpoint not specified");
                }

            case TransportProtocol.Tcp:
                string host;
                int port;
                connectionStringBuilder.ParseTcpSocketPath(out host, out port);
                this.metricDataTransport = new MetricTcpDataTransport(host, port, options.OnTcpConnectionSuccess, options.OnTcpConnectionFailure);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

        unsafe
        {
            this.fixedPayloadStartIndex = sizeof(BinaryHeader);
        }

        if (connectionStringBuilder.DisableMetricNameValidation)
        {
            DisableOpenTelemetrySdkMetricNameValidation();
        }
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        string monitoringAccount = this.defaultMonitoringAccount;
        string metricNamespace = this.defaultMetricNamespace;

        var result = ExportResult.Success;
        foreach (var metric in batch)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                try
                {
#if EXPOSE_EXPERIMENTAL_FEATURES
                    var exemplars = metricPoint.GetExemplars();
#endif

                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                            {
                                var ulongSum = Convert.ToUInt64(metricPoint.GetSumLong());
                                var metricData = new MetricData { UInt64Value = ulongSum };
                                var bodyLength = this.SerializeMetricWithTLV(
                                    MetricEventType.ULongMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(), // Using the endTime here as the timestamp as Geneva Metrics only allows for one field for timestamp
                                    metricPoint.Tags,
                                    metricData,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }

                        // The value here could be negative hence we have to use `MetricEventType.DoubleMetric`
                        case MetricType.LongGauge:
                            {
                                // potential for minor precision loss implicitly going from long->double
                                // see: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#implicit-numeric-conversions
                                var doubleSum = Convert.ToDouble(metricPoint.GetGaugeLastValueLong());
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetricWithTLV(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }

                        // The value here could be negative hence we have to use `MetricEventType.DoubleMetric`
                        case MetricType.LongSumNonMonotonic:
                            {
                                // potential for minor precision loss implicitly going from long->double
                                // see: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions#implicit-numeric-conversions
                                var doubleSum = Convert.ToDouble(metricPoint.GetSumLong());
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetricWithTLV(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }

                        case MetricType.DoubleSum:
                        case MetricType.DoubleSumNonMonotonic:
                            {
                                var doubleSum = metricPoint.GetSumDouble();
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetricWithTLV(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }

                        case MetricType.DoubleGauge:
                            {
                                var doubleSum = metricPoint.GetGaugeLastValueDouble();
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetricWithTLV(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }

                        case MetricType.Histogram:
                            {
                                var sum = Convert.ToUInt64(metricPoint.GetHistogramSum());
                                var count = Convert.ToUInt32(metricPoint.GetHistogramCount());
                                if (!metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                                {
                                    min = 0;
                                    max = 0;
                                }

                                var bodyLength = this.SerializeHistogramMetricWithTLV(
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricPoint.GetHistogramBuckets(),
                                    sum,
                                    count,
                                    min,
                                    max,
#if EXPOSE_EXPERIMENTAL_FEATURES
                                    exemplars,
#endif
                                    out monitoringAccount,
                                    out metricNamespace);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }
                    }
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

    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                this.metricDataTransport?.Dispose();
            }
            catch (Exception ex)
            {
                ExporterEventSource.Log.ExporterException("GenevaMetricExporter Dispose failed.", ex);
            }
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

    internal unsafe ushort SerializeMetricWithTLV(
        MetricEventType eventType,
        string metricName,
        long timestamp,
        in ReadOnlyTagCollection tags,
        MetricData value,
#if EXPOSE_EXPERIMENTAL_FEATURES
        Exemplar[] exemplars,
#endif
        out string monitoringAccount,
        out string metricNamespace)
    {
        ushort bodyLength;
        try
        {
            // The buffer format is as follows:
            // -- BinaryHeader
            // -- Sequence of payload types

            // Leave enough space for the header
            var bufferIndex = sizeof(BinaryHeader);

            SerializeMetricName(metricName, this.buffer, ref bufferIndex);

            SerializeNonHistogramMetricData(eventType, value, timestamp, this.buffer, ref bufferIndex);

            // Serializes metric dimensions and also gets the custom account name and metric namespace
            // if specified by adding custom tags: _microsoft_metrics_namespace and _microsoft_metrics_namespace
            this.SerializeDimensionsAndGetCustomAccountNamespace(
                tags,
                this.buffer,
                ref bufferIndex,
                out monitoringAccount,
                out metricNamespace);

#if EXPOSE_EXPERIMENTAL_FEATURES
            SerializeExemplars(exemplars, this.buffer, ref bufferIndex);
#endif

            SerializeMonitoringAccount(monitoringAccount, this.buffer, ref bufferIndex);

            SerializeMetricNamespace(metricNamespace, this.buffer, ref bufferIndex);

            // Write the final size of the payload
            bodyLength = (ushort)(bufferIndex - this.fixedPayloadStartIndex);

            // Copy in the final structures to the front
            fixed (byte* bufferBytes = this.buffer)
            {
                var ptr = (BinaryHeader*)bufferBytes;
                ptr->EventId = (ushort)MetricEventType.TLV;
                ptr->BodyLength = bodyLength;
            }
        }
        finally
        {
        }

        return bodyLength;
    }

    internal unsafe ushort SerializeHistogramMetricWithTLV(
        string metricName,
        long timestamp,
        in ReadOnlyTagCollection tags,
        HistogramBuckets buckets,
        double sum,
        uint count,
        double min,
        double max,
#if EXPOSE_EXPERIMENTAL_FEATURES
        Exemplar[] exemplars,
#endif
        out string monitoringAccount,
        out string metricNamespace)
    {
        ushort bodyLength;
        try
        {
            // The buffer format is as follows:
            // -- BinaryHeader
            // -- Sequence of payload types

            // Leave enough space for the header
            var bufferIndex = sizeof(BinaryHeader);

            SerializeMetricName(metricName, this.buffer, ref bufferIndex);

            SerializeHistogramMetricData(buckets, sum, count, min, max, timestamp, this.buffer, ref bufferIndex);

            // Serializes metric dimensions and also gets the custom account name and metric namespace
            // if specified by adding custom tags: _microsoft_metrics_namespace and _microsoft_metrics_namespace
            this.SerializeDimensionsAndGetCustomAccountNamespace(
                tags,
                this.buffer,
                ref bufferIndex,
                out monitoringAccount,
                out metricNamespace);

#if EXPOSE_EXPERIMENTAL_FEATURES
            SerializeExemplars(exemplars, this.buffer, ref bufferIndex);
#endif

            SerializeMonitoringAccount(monitoringAccount, this.buffer, ref bufferIndex);

            SerializeMetricNamespace(metricNamespace, this.buffer, ref bufferIndex);

            // Write the final size of the payload
            bodyLength = (ushort)(bufferIndex - this.fixedPayloadStartIndex);

            // Copy in the final structures to the front
            fixed (byte* bufferBytes = this.buffer)
            {
                var ptr = (BinaryHeader*)bufferBytes;
                ptr->EventId = (ushort)MetricEventType.TLV;
                ptr->BodyLength = bodyLength;
            }
        }
        finally
        {
        }

        return bodyLength;
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

#if EXPOSE_EXPERIMENTAL_FEATURES
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeExemplars(Exemplar[] exemplars, byte[] buffer, ref int bufferIndex)
    {
        if (exemplars.Length > 0)
        {
            MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.Exemplars);

            // Get a placeholder to add the payloadType length
            var payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            MetricSerializer.SerializeByte(buffer, ref bufferIndex, 0); // version

            // TODO: Avoid this additional enumeration
            var exemplarsCount = 0;
            foreach (var exemplar in exemplars)
            {
                if (exemplar.Timestamp != default)
                {
                    exemplarsCount++;
                }
            }

            MetricSerializer.SerializeInt32AsBase128(buffer, ref bufferIndex, exemplarsCount);

            foreach (var exemplar in exemplars)
            {
                if (exemplar.Timestamp != default)
                {
                    SerializeSingleExmeplar(exemplar, buffer, ref bufferIndex);
                }
            }

            var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(buffer, ref payloadTypeStartIndex, payloadTypeLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeSingleExmeplar(Exemplar exemplar, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, 0); // version

        var bufferIndexForLength = bufferIndex;
        bufferIndex++;

        var bufferIndexForFlags = bufferIndex;
        bufferIndex++;

        var flags = ExemplarFlags.IsTimestampAvailable; // we only serialize exemplars with Timestamp != default

        // TODO: Update the code when Exemplars support long values
        var value = exemplar.DoubleValue;

        // Check if the double value is actually a whole number that can be serialized as a long instead
        var valueAsLong = (long)value;
        bool isWholeNumber = valueAsLong == value;
        if (isWholeNumber)
        {
            flags |= ExemplarFlags.IsMetricValueDoubleStoredAsLong;
            MetricSerializer.SerializeInt64AsBase128(buffer, ref bufferIndex, valueAsLong); // serialize long value
        }
        else
        {
            MetricSerializer.SerializeFloat64(buffer, ref bufferIndex, value); // serialize double value
        }

        var bufferIndexForNumberOfLabels = bufferIndex;
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, 0); // serialize zero as the count of labels; this would be updated later if the exemplar has labels
        byte numberOfLabels = 0;

        // Convert exemplar timestamp to unix nanoseconds
        var unixNanoSeconds = DateTime.FromFileTimeUtc(exemplar.Timestamp.ToFileTime())
                        .ToUniversalTime()
                        .Subtract(new DateTime(1970, 1, 1))
                        .TotalMilliseconds * 1000000;

        MetricSerializer.SerializeInt64(buffer, ref bufferIndex, (long)unixNanoSeconds); // serialize timestamp

        if (exemplar.TraceId.HasValue)
        {
            Span<byte> traceIdBytes = stackalloc byte[16];
            exemplar.TraceId.Value.CopyTo(traceIdBytes);
            MetricSerializer.SerializeSpanOfBytes(buffer, ref bufferIndex, traceIdBytes, traceIdBytes.Length); // serialize traceId

            flags |= ExemplarFlags.TraceIdExists;
        }

        if (exemplar.SpanId.HasValue)
        {
            Span<byte> spanIdBytes = stackalloc byte[8];
            exemplar.SpanId.Value.CopyTo(spanIdBytes);
            MetricSerializer.SerializeSpanOfBytes(buffer, ref bufferIndex, spanIdBytes, spanIdBytes.Length); // serialize spanId

            flags |= ExemplarFlags.SpanIdExists;
        }

        bool hasLabels = exemplar.FilteredTags != null && exemplar.FilteredTags.Count > 0;
        if (hasLabels)
        {
            foreach (var tag in exemplar.FilteredTags)
            {
                MetricSerializer.SerializeBase128String(buffer, ref bufferIndex, tag.Key);
                MetricSerializer.SerializeBase128String(buffer, ref bufferIndex, Convert.ToString(tag.Value, CultureInfo.InvariantCulture));
                numberOfLabels++;
            }

            MetricSerializer.SerializeByte(buffer, ref bufferIndexForNumberOfLabels, numberOfLabels);
        }

        MetricSerializer.SerializeByte(buffer, ref bufferIndexForFlags, (byte)flags);

        var exemplarLength = bufferIndex - bufferIndexForLength + 1;
        MetricSerializer.SerializeByte(buffer, ref bufferIndexForLength, (byte)exemplarLength);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeNonHistogramMetricData(MetricEventType eventType, MetricData value, long timestamp, byte[] buffer, ref int bufferIndex)
    {
        var payloadType = eventType == MetricEventType.ULongMetric ? PayloadType.ULongMetric : PayloadType.DoubleMetric;
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)payloadType);

        // Get a placeholder to add the payloadType length
        int payloadTypeStartIndex = bufferIndex;
        bufferIndex += 2;

        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, (ulong)timestamp); // timestamp

        if (payloadType == PayloadType.ULongMetric)
        {
            MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, value.UInt64Value);
        }
        else
        {
            MetricSerializer.SerializeFloat64(buffer, ref bufferIndex, value.DoubleValue);
        }

        var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
        MetricSerializer.SerializeUInt16(buffer, ref payloadTypeStartIndex, payloadTypeLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeHistogramMetricData(HistogramBuckets buckets, double sum, uint count, double min, double max, long timestamp, byte[] buffer, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.ExternallyAggregatedULongDistributionMetric);

        // Get a placeholder to add the payloadType length
        int payloadTypeStartIndex = bufferIndex;
        bufferIndex += 2;

        // Serialize sum, count, min, and max
        MetricSerializer.SerializeUInt32(buffer, ref bufferIndex, count); // histogram count
        MetricSerializer.SerializeUInt32(buffer, ref bufferIndex, 0); // padding
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, (ulong)timestamp); // timestamp
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(sum)); // histogram sum
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(min)); // histogram min
        MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(max)); // histogram max

        var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
        MetricSerializer.SerializeUInt16(buffer, ref payloadTypeStartIndex, payloadTypeLength);

        // Serialize histogram buckets as value-count pairs
        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.HistogramULongValueCountPairs);

        // Get a placeholder to add the payloadType length
        payloadTypeStartIndex = bufferIndex;
        bufferIndex += 2;

        // Get a placeholder to add the number of value-count pairs added
        // with value being the bucket boundary and count being the respective count

        var itemsWrittenIndex = bufferIndex;
        MetricSerializer.SerializeUInt16(buffer, ref bufferIndex, 0);

        // Bucket values
        ushort bucketCount = 0;
        double lastExplicitBound = default;
        foreach (var bucket in buckets)
        {
            if (bucket.BucketCount > 0)
            {
                SerializeHistogramBucketWithTLV(bucket, buffer, ref bufferIndex, lastExplicitBound);
                bucketCount++;
            }

            lastExplicitBound = bucket.ExplicitBound;
        }

        // Write the number of items in distribution emitted and reset back to end.
        MetricSerializer.SerializeUInt16(buffer, ref itemsWrittenIndex, bucketCount);

        payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
        MetricSerializer.SerializeUInt16(buffer, ref payloadTypeStartIndex, payloadTypeLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeHistogramBucketWithTLV(in HistogramBucket bucket, byte[] buffer, ref int bufferIndex, double lastExplicitBound)
    {
        if (bucket.ExplicitBound != double.PositiveInfinity)
        {
            MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(bucket.ExplicitBound));
        }
        else
        {
            // The bucket to catch the overflows is one greater than the last bound provided
            MetricSerializer.SerializeUInt64(buffer, ref bufferIndex, Convert.ToUInt64(lastExplicitBound + 1));
        }

        MetricSerializer.SerializeUInt32(buffer, ref bufferIndex, Convert.ToUInt32(bucket.BucketCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeDimensionsAndGetCustomAccountNamespace(in ReadOnlyTagCollection tags, byte[] buffer, ref int bufferIndex, out string monitoringAccount, out string metricNamespace)
    {
        monitoringAccount = this.defaultMonitoringAccount;
        metricNamespace = this.defaultMetricNamespace;

        MetricSerializer.SerializeByte(buffer, ref bufferIndex, (byte)PayloadType.Dimensions);

        // Get a placeholder to add the payloadType length
        var payloadTypeStartIndex = bufferIndex;
        bufferIndex += 2;

        // Get a placeholder to add dimensions count later
        var bufferIndexForDimensionsCount = bufferIndex;
        bufferIndex += 2;

        ushort dimensionsWritten = 0;

        // Serialize PrepopulatedDimensions keys
        for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
        {
            MetricSerializer.SerializeEncodedString(buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsKeys[i]);
        }

        if (this.prepopulatedDimensionsCount > 0)
        {
            dimensionsWritten += this.prepopulatedDimensionsCount;
        }

        int reservedTags = 0;

        // Serialize MetricPoint Dimension keys
        foreach (var tag in tags)
        {
            if (tag.Key.Length > MaxDimensionNameSize)
            {
                // TODO: Data Validation
            }

            if (tag.Key.Equals(DimensionKeyForCustomMonitoringAccount, StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Equals(DimensionKeyForCustomMetricsNamespace, StringComparison.OrdinalIgnoreCase))
            {
                reservedTags++;
                continue;
            }

            MetricSerializer.SerializeString(buffer, ref bufferIndex, tag.Key);
        }

        dimensionsWritten += (ushort)(tags.Count - reservedTags);

        // Serialize PrepopulatedDimensions values
        for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
        {
            MetricSerializer.SerializeEncodedString(buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsValues[i]);
        }

        // Serialize MetricPoint Dimension values
        foreach (var tag in tags)
        {
            if (tag.Key.Equals(DimensionKeyForCustomMonitoringAccount, StringComparison.OrdinalIgnoreCase) && tag.Value is string metricsAccount)
            {
                if (!string.IsNullOrWhiteSpace(metricsAccount))
                {
                    monitoringAccount = metricsAccount;
                }

                continue;
            }

            if (tag.Key.Equals(DimensionKeyForCustomMetricsNamespace, StringComparison.OrdinalIgnoreCase) && tag.Value is string metricsNamespace)
            {
                if (!string.IsNullOrWhiteSpace(metricsNamespace))
                {
                    metricNamespace = metricsNamespace;
                }

                continue;
            }

            var dimensionValue = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
            if (dimensionValue.Length > MaxDimensionValueSize)
            {
                // TODO: Data Validation
            }

            MetricSerializer.SerializeString(buffer, ref bufferIndex, dimensionValue);
        }

        // Backfill the number of dimensions written
        MetricSerializer.SerializeUInt16(buffer, ref bufferIndexForDimensionsCount, dimensionsWritten);

        var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
        MetricSerializer.SerializeUInt16(buffer, ref payloadTypeStartIndex, payloadTypeLength);
    }

    private List<byte[]> SerializePrepopulatedDimensionsKeys(IEnumerable<string> keys)
    {
        var serializedKeys = new List<byte[]>(this.prepopulatedDimensionsCount);
        foreach (var key in keys)
        {
            serializedKeys.Add(Encoding.UTF8.GetBytes(key));
        }

        return serializedKeys;
    }

    private List<byte[]> SerializePrepopulatedDimensionsValues(IEnumerable<object> values)
    {
        var serializedValues = new List<byte[]>(this.prepopulatedDimensionsCount);
        foreach (var value in values)
        {
            var valueAsString = Convert.ToString(value, CultureInfo.InvariantCulture);
            serializedValues.Add(Encoding.UTF8.GetBytes(valueAsString));
        }

        return serializedValues;
    }
}

// <copyright file="GenevaMetricExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

    private readonly ushort prepopulatedDimensionsCount;

    private readonly int fixedPayloadStartIndex;

    private readonly string monitoringAccount;

    private readonly string metricNamespace;

    private readonly IMetricDataTransport metricDataTransport;

    private readonly List<byte[]> serializedPrepopulatedDimensionsKeys;

    private readonly List<byte[]> serializedPrepopulatedDimensionsValues;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly byte[] bufferForNonHistogramMetrics = new byte[BufferSize];

    private readonly byte[] bufferForHistogramMetrics = new byte[BufferSize];

    private readonly int bufferIndexForNonHistogramMetrics;

    private readonly int bufferIndexForHistogramMetrics;

    private bool isDisposed;

    public GenevaMetricExporter(GenevaMetricExporterOptions options)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNullOrWhitespace(options.ConnectionString);

        var connectionStringBuilder = new ConnectionStringBuilder(options.ConnectionString);
        this.monitoringAccount = connectionStringBuilder.Account;
        this.metricNamespace = connectionStringBuilder.Namespace;

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
                    this.metricDataTransport = new MetricEtwDataTransport();
                    break;
                }
                else
                {
                    throw new ArgumentException("Endpoint not specified");
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(connectionStringBuilder.Protocol));
        }

        this.bufferIndexForNonHistogramMetrics = this.InitializeBufferForNonHistogramMetrics();
        this.bufferIndexForHistogramMetrics = this.InitializeBufferForHistogramMetrics();

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
        var result = ExportResult.Success;
        foreach (var metric in batch)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                try
                {
                    var exemplars = metricPoint.GetExemplars();

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
                                    exemplars);
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
                                    exemplars);
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
                                    exemplars);
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
                                    exemplars);
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
                                    exemplars);
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
                                    exemplars);
                                this.metricDataTransport.Send(MetricEventType.TLV, this.buffer, bodyLength);
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    ExporterEventSource.Log.FailedToSendMetricData(this.monitoringAccount, this.metricNamespace, metric.Name, ex); // TODO: preallocate exception or no exception
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

    internal unsafe ushort SerializeMetric(
        MetricEventType eventType,
        string metricName,
        long timestamp,
        in ReadOnlyTagCollection tags,
        MetricData value)
    {
        ushort bodyLength;
        try
        {
            var bufferIndex = this.bufferIndexForNonHistogramMetrics;
            MetricSerializer.SerializeString(this.bufferForNonHistogramMetrics, ref bufferIndex, metricName);

            ushort dimensionsWritten = 0;

            // Serialize PrepopulatedDimensions keys
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.bufferForNonHistogramMetrics, ref bufferIndex, this.serializedPrepopulatedDimensionsKeys[i]);
            }

            if (this.prepopulatedDimensionsCount > 0)
            {
                dimensionsWritten += this.prepopulatedDimensionsCount;
            }

            // Serialize MetricPoint Dimension keys
            foreach (var tag in tags)
            {
                if (tag.Key.Length > MaxDimensionNameSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.bufferForNonHistogramMetrics, ref bufferIndex, tag.Key);
            }

            dimensionsWritten += (ushort)tags.Count;

            // Serialize PrepopulatedDimensions values
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.bufferForNonHistogramMetrics, ref bufferIndex, this.serializedPrepopulatedDimensionsValues[i]);
            }

            // Serialize MetricPoint Dimension values
            foreach (var tag in tags)
            {
                var dimensionValue = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
                if (dimensionValue.Length > MaxDimensionValueSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.bufferForNonHistogramMetrics, ref bufferIndex, dimensionValue);
            }

            // The Autopilot container name is optional but still preserve the location with zero length if it is empty.
            MetricSerializer.SerializeInt16(this.bufferForNonHistogramMetrics, ref bufferIndex, 0);

            // Write the final size of the payload
            bodyLength = (ushort)(bufferIndex - this.fixedPayloadStartIndex);

            // Copy in the final structures to the front
            fixed (byte* bufferBytes = this.bufferForNonHistogramMetrics)
            {
                var ptr = (BinaryHeader*)bufferBytes;
                ptr->EventId = (ushort)eventType;
                ptr->BodyLength = bodyLength;

                var payloadPtr = (MetricPayload*)&bufferBytes[this.fixedPayloadStartIndex];
                payloadPtr->CountDimension = dimensionsWritten;
                payloadPtr->ReservedWord = 0;
                payloadPtr->ReservedDword = 0;
                payloadPtr->TimestampUtc = (ulong)timestamp;
                payloadPtr->Data = value;
            }
        }
        finally
        {
        }

        return bodyLength;
    }

    internal unsafe ushort SerializeHistogramMetric(
        string metricName,
        long timestamp,
        in ReadOnlyTagCollection tags,
        HistogramBuckets buckets,
        MetricData sum,
        uint count,
        MetricData min,
        MetricData max)
    {
        ushort bodyLength;
        try
        {
            var bufferIndex = this.bufferIndexForHistogramMetrics;
            MetricSerializer.SerializeString(this.bufferForHistogramMetrics, ref bufferIndex, metricName);

            ushort dimensionsWritten = 0;

            // Serialize PrepopulatedDimensions keys
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.bufferForHistogramMetrics, ref bufferIndex, this.serializedPrepopulatedDimensionsKeys[i]);
            }

            if (this.prepopulatedDimensionsCount > 0)
            {
                dimensionsWritten += this.prepopulatedDimensionsCount;
            }

            // Serialize MetricPoint Dimension keys
            foreach (var tag in tags)
            {
                if (tag.Key.Length > MaxDimensionNameSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.bufferForHistogramMetrics, ref bufferIndex, tag.Key);
            }

            dimensionsWritten += (ushort)tags.Count;

            // Serialize PrepopulatedDimensions values
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.bufferForHistogramMetrics, ref bufferIndex, this.serializedPrepopulatedDimensionsValues[i]);
            }

            // Serialize MetricPoint Dimension values
            foreach (var tag in tags)
            {
                var dimensionValue = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
                if (dimensionValue.Length > MaxDimensionValueSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.bufferForHistogramMetrics, ref bufferIndex, dimensionValue);
            }

            // The Autopilot container name is optional but still preserve the location with zero length if it is empty.
            MetricSerializer.SerializeInt16(this.bufferForHistogramMetrics, ref bufferIndex, 0);

            // Version
            MetricSerializer.SerializeByte(this.bufferForHistogramMetrics, ref bufferIndex, 0);

            // Meta-data
            // Value-count pairs is associated with the constant value of 2 in the distribution_type enum.
            MetricSerializer.SerializeByte(this.bufferForHistogramMetrics, ref bufferIndex, 2);

            // Keep a position to record how many buckets are added
            var itemsWrittenIndex = bufferIndex;
            MetricSerializer.SerializeUInt16(this.bufferForHistogramMetrics, ref bufferIndex, 0);

            // Bucket values
            ushort bucketCount = 0;
            double lastExplicitBound = default;
            foreach (var bucket in buckets)
            {
                if (bucket.BucketCount > 0)
                {
                    this.SerializeHistogramBucket(bucket, ref bufferIndex, lastExplicitBound);
                    bucketCount++;
                }

                lastExplicitBound = bucket.ExplicitBound;
            }

            // Write the number of items in distribution emitted and reset back to end.
            MetricSerializer.SerializeUInt16(this.bufferForHistogramMetrics, ref itemsWrittenIndex, bucketCount);

            // Write the final size of the payload
            bodyLength = (ushort)(bufferIndex - this.fixedPayloadStartIndex);

            // Copy in the final structures to the front
            fixed (byte* bufferBytes = this.bufferForHistogramMetrics)
            {
                var ptr = (BinaryHeader*)bufferBytes;
                ptr->EventId = (ushort)MetricEventType.ExternallyAggregatedULongDistributionMetric;
                ptr->BodyLength = bodyLength;

                var payloadPtr = (ExternalPayload*)&bufferBytes[this.fixedPayloadStartIndex];
                payloadPtr[0].CountDimension = dimensionsWritten;
                payloadPtr[0].ReservedWord = 0;
                payloadPtr[0].Count = count;
                payloadPtr[0].TimestampUtc = (ulong)timestamp;
                payloadPtr[0].Sum = sum;
                payloadPtr[0].Min = min;
                payloadPtr[0].Max = max;
            }
        }
        finally
        {
        }

        return bodyLength;
    }

    internal unsafe ushort SerializeMetricWithTLV(
        MetricEventType eventType,
        string metricName,
        long timestamp,
        in ReadOnlyTagCollection tags,
        MetricData value,
        Exemplar[] exemplars)
    {
        ushort bodyLength;
        try
        {
            // The buffer format is as follows:
            // -- BinaryHeader
            // -- Sequence of payload types

            // Leave enough space for the header
            var bufferIndex = sizeof(BinaryHeader);

            // Serialize metric name
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.MetricName);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(metricName));

            #region Serialize metric data

            var payloadType = eventType == MetricEventType.ULongMetric ? PayloadType.ULongMetric : PayloadType.DoubleMetric;
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)payloadType);

            // Get a placeholder to add the payloadType length
            int payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, (ulong)timestamp); // timestamp

            if (payloadType == PayloadType.ULongMetric)
            {
                MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, value.UInt64Value);
            }
            else
            {
                MetricSerializer.SerializeFloat64(this.buffer, ref bufferIndex, value.DoubleValue);
            }

            var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            #region Serialize metric dimensions
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Dimensions);

            // Get a placeholder to add the payloadType length
            payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            // Get a placeholder to add dimensions count later
            var bufferIndexForDimensionsCount = bufferIndex;
            bufferIndex += 2;

            ushort dimensionsWritten = 0;

            // Serialize PrepopulatedDimensions keys
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsKeys[i]);
            }

            if (this.prepopulatedDimensionsCount > 0)
            {
                dimensionsWritten += this.prepopulatedDimensionsCount;
            }

            // Serialize MetricPoint Dimension keys
            foreach (var tag in tags)
            {
                if (tag.Key.Length > MaxDimensionNameSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.buffer, ref bufferIndex, tag.Key);
            }

            dimensionsWritten += (ushort)tags.Count;

            // Serialize PrepopulatedDimensions values
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsValues[i]);
            }

            // Serialize MetricPoint Dimension values
            foreach (var tag in tags)
            {
                var dimensionValue = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
                if (dimensionValue.Length > MaxDimensionValueSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.buffer, ref bufferIndex, dimensionValue);
            }

            // Backfill the number of dimensions written
            MetricSerializer.SerializeUInt16(this.buffer, ref bufferIndexForDimensionsCount, dimensionsWritten);

            payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            #region Serialize exemplars

            if (exemplars.Length > 0)
            {
                MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Exemplars);

                // Get a placeholder to add the payloadType length
                payloadTypeStartIndex = bufferIndex;
                bufferIndex += 2;

                MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, 0); // version

                var exemplarsCount = 0;
                foreach (var exemplar in exemplars)
                {
                    if (exemplar.Timestamp != default)
                    {
                        exemplarsCount++;
                    }
                }

                MetricSerializer.SerializeInt32AsBase128(this.buffer, ref bufferIndex, exemplarsCount);

                foreach (var exemplar in exemplars)
                {
                    if (exemplar.Timestamp != default)
                    {
                        this.SerializeExemplar(exemplar, ref bufferIndex);
                    }
                }
            }

            payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            // Serialize monitoring account
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.AccountName);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(this.monitoringAccount));

            // Serialize metric namespace
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Namespace);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(this.metricNamespace));

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
        Exemplar[] exemplars)
    {
        ushort bodyLength;
        try
        {
            // The buffer format is as follows:
            // -- BinaryHeader
            // -- Sequence of payload types

            // Leave enough space for the header
            var bufferIndex = sizeof(BinaryHeader);

            // Serialize metric name
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.MetricName);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(metricName));

            #region Serialize histogram metric data

            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.ExternallyAggregatedULongDistributionMetric);

            // Get a placeholder to add the payloadType length
            int payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            // Serialize sum, count, min, and max
            MetricSerializer.SerializeUInt32(this.buffer, ref bufferIndex, count); // histogram count
            MetricSerializer.SerializeUInt32(this.buffer, ref bufferIndex, 0); // padding
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, (ulong)timestamp); // timestamp
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, Convert.ToUInt64(sum)); // histogram sum
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, Convert.ToUInt64(min)); // histogram min
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, Convert.ToUInt64(max)); // histogram max

            var payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            // Serialize histogram buckets as value-count pairs
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.HistogramULongValueCountPairs);

            // Get a placeholder to add the payloadType length
            payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            // Get a placeholder to add the number of value-count pairs added
            // with value being the bucket boundary and count being the respective count

            var itemsWrittenIndex = bufferIndex;
            MetricSerializer.SerializeUInt16(this.buffer, ref bufferIndex, 0);

            // Bucket values
            ushort bucketCount = 0;
            double lastExplicitBound = default;
            foreach (var bucket in buckets)
            {
                if (bucket.BucketCount > 0)
                {
                    this.SerializeHistogramBucketWithTLV(bucket, ref bufferIndex, lastExplicitBound);
                    bucketCount++;
                }

                lastExplicitBound = bucket.ExplicitBound;
            }

            // Write the number of items in distribution emitted and reset back to end.
            MetricSerializer.SerializeUInt16(this.buffer, ref itemsWrittenIndex, bucketCount);

            payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            #region Serialize metric dimensions
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Dimensions);

            // Get a placeholder to add the payloadType length
            payloadTypeStartIndex = bufferIndex;
            bufferIndex += 2;

            // Get a placeholder to add dimensions count later
            var bufferIndexForDimensionsCount = bufferIndex;
            bufferIndex += 2;

            ushort dimensionsWritten = 0;

            // Serialize PrepopulatedDimensions keys
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsKeys[i]);
            }

            if (this.prepopulatedDimensionsCount > 0)
            {
                dimensionsWritten += this.prepopulatedDimensionsCount;
            }

            // Serialize MetricPoint Dimension keys
            foreach (var tag in tags)
            {
                if (tag.Key.Length > MaxDimensionNameSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.buffer, ref bufferIndex, tag.Key);
            }

            dimensionsWritten += (ushort)tags.Count;

            // Serialize PrepopulatedDimensions values
            for (ushort i = 0; i < this.prepopulatedDimensionsCount; i++)
            {
                MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, this.serializedPrepopulatedDimensionsValues[i]);
            }

            // Serialize MetricPoint Dimension values
            foreach (var tag in tags)
            {
                var dimensionValue = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
                if (dimensionValue.Length > MaxDimensionValueSize)
                {
                    // TODO: Data Validation
                }

                MetricSerializer.SerializeString(this.buffer, ref bufferIndex, dimensionValue);
            }

            // Backfill the number of dimensions written
            MetricSerializer.SerializeUInt16(this.buffer, ref bufferIndexForDimensionsCount, dimensionsWritten);

            payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            #region Serialize exemplars

            if (exemplars.Length > 0)
            {
                MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Exemplars);

                // Get a placeholder to add the payloadType length
                payloadTypeStartIndex = bufferIndex;
                bufferIndex += 2;

                MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, 0); // version

                var exemplarsCount = 0;
                foreach (var exemplar in exemplars)
                {
                    if (exemplar.Timestamp != default)
                    {
                        exemplarsCount++;
                    }
                }

                MetricSerializer.SerializeInt32AsBase128(this.buffer, ref bufferIndex, exemplarsCount);

                foreach (var exemplar in exemplars)
                {
                    if (exemplar.Timestamp != default)
                    {
                        this.SerializeExemplar(exemplar, ref bufferIndex);
                    }
                }
            }

            payloadTypeLength = (ushort)(bufferIndex - payloadTypeStartIndex - 2);
            MetricSerializer.SerializeUInt16(this.buffer, ref payloadTypeStartIndex, payloadTypeLength);

            #endregion

            // Serialize monitoring account
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.AccountName);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(this.monitoringAccount));

            // Serialize metric namespace
            MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, (byte)PayloadType.Namespace);
            MetricSerializer.SerializeEncodedString(this.buffer, ref bufferIndex, Encoding.UTF8.GetBytes(this.metricNamespace));

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
    private void SerializeHistogramBucket(in HistogramBucket bucket, ref int bufferIndex, double lastExplicitBound)
    {
        if (bucket.ExplicitBound != double.PositiveInfinity)
        {
            MetricSerializer.SerializeUInt64(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt64(bucket.ExplicitBound));
        }
        else
        {
            // The bucket to catch the overflows is one greater than the last bound provided
            MetricSerializer.SerializeUInt64(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt64(lastExplicitBound + 1));
        }

        MetricSerializer.SerializeUInt32(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt32(bucket.BucketCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeHistogramBucketWithTLV(in HistogramBucket bucket, ref int bufferIndex, double lastExplicitBound)
    {
        if (bucket.ExplicitBound != double.PositiveInfinity)
        {
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, Convert.ToUInt64(bucket.ExplicitBound));
        }
        else
        {
            // The bucket to catch the overflows is one greater than the last bound provided
            MetricSerializer.SerializeUInt64(this.buffer, ref bufferIndex, Convert.ToUInt64(lastExplicitBound + 1));
        }

        MetricSerializer.SerializeUInt32(this.buffer, ref bufferIndex, Convert.ToUInt32(bucket.BucketCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SerializeExemplar(Exemplar exemplar, ref int bufferIndex)
    {
        MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, 0); // version

        var bufferIndexForLength = bufferIndex;
        bufferIndex++;

        var bufferIndexForFlags = bufferIndex;
        bufferIndex++;

        var flags = ExemplarFlags.IsTimestampAvailable; // we only serialize exemplars with Timestamp != default

        // TODO: Update the code whenn Exemplars support long values
        var value = exemplar.DoubleValue;

        // Check if the double value is actually a whole number that can be serialized as a long instead
        var valueAsLong = (long)value;
        bool isWholeNumber = valueAsLong == value;
        if (isWholeNumber)
        {
            flags |= ExemplarFlags.IsMetricValueDoubleStoredAsLong;
            MetricSerializer.SerializeInt64AsBase128(this.buffer, ref bufferIndex, valueAsLong); // serialize long value
        }
        else
        {
            MetricSerializer.SerializeFloat64(this.buffer, ref bufferIndex, value); // serialize double value
        }

        var bufferIndexForNumberOfLabels = bufferIndex;
        MetricSerializer.SerializeByte(this.buffer, ref bufferIndex, 0); // serialize zero as the count of labels; this would be updated later if the exemplar has labels
        byte numberOfLabels = 0;

        // Convert exemplar timestamp to unix nanoseconds
        var unixNanoSeconds = DateTime.FromFileTimeUtc(exemplar.Timestamp.ToFileTime())
                        .ToUniversalTime()
                        .Subtract(new DateTime(1970, 1, 1))
                        .TotalMilliseconds * 1000000;

        MetricSerializer.SerializeInt64(this.buffer, ref bufferIndex, (long)unixNanoSeconds); // serialize timestamp

        if (exemplar.TraceId.HasValue)
        {
            Span<byte> traceIdBytes = stackalloc byte[16];
            exemplar.TraceId.Value.CopyTo(traceIdBytes);
            MetricSerializer.SerializeSpanOfBytes(this.buffer, ref bufferIndex, traceIdBytes, traceIdBytes.Length); // serialize traceId

            flags |= ExemplarFlags.TraceIdExists;
        }

        if (exemplar.SpanId.HasValue)
        {
            Span<byte> spanIdBytes = stackalloc byte[8];
            exemplar.SpanId.Value.CopyTo(spanIdBytes);
            MetricSerializer.SerializeSpanOfBytes(this.buffer, ref bufferIndex, spanIdBytes, spanIdBytes.Length); // serialize spanId

            flags |= ExemplarFlags.SpanIdExists;
        }

        bool hasLabels = exemplar.FilteredTags != null && exemplar.FilteredTags.Count > 0;
        if (hasLabels)
        {
            foreach (var tag in exemplar.FilteredTags)
            {
                MetricSerializer.SerializeBase128String(this.buffer, ref bufferIndex, tag.Key);
                MetricSerializer.SerializeBase128String(this.buffer, ref bufferIndex, Convert.ToString(tag.Value, CultureInfo.InvariantCulture));
                numberOfLabels++;
            }

            MetricSerializer.SerializeByte(this.buffer, ref bufferIndexForNumberOfLabels, numberOfLabels);
        }

        MetricSerializer.SerializeByte(this.buffer, ref bufferIndexForFlags, (byte)flags);

        var exemplarLength = bufferIndex - bufferIndexForLength + 1;
        MetricSerializer.SerializeByte(this.buffer, ref bufferIndexForLength, (byte)exemplarLength);
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

    private unsafe int InitializeBufferForNonHistogramMetrics()
    {
        // The buffer format is as follows:
        // -- BinaryHeader
        // -- MetricPayload
        // -- Variable length content

        // Leave enough space for the header and fixed payload
        var bufferIndex = sizeof(BinaryHeader) + sizeof(MetricPayload);

        MetricSerializer.SerializeString(this.bufferForNonHistogramMetrics, ref bufferIndex, this.monitoringAccount);
        MetricSerializer.SerializeString(this.bufferForNonHistogramMetrics, ref bufferIndex, this.metricNamespace);

        return bufferIndex;
    }

    private unsafe int InitializeBufferForHistogramMetrics()
    {
        // The buffer format is as follows:
        // -- BinaryHeader
        // -- ExternalPayload
        // -- Variable length content

        // Leave enough space for the header and fixed payload
        var bufferIndex = sizeof(BinaryHeader) + sizeof(ExternalPayload);

        MetricSerializer.SerializeString(this.bufferForHistogramMetrics, ref bufferIndex, this.monitoringAccount);
        MetricSerializer.SerializeString(this.bufferForHistogramMetrics, ref bufferIndex, this.metricNamespace);

        return bufferIndex;
    }
}

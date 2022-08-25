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
using System.Runtime.InteropServices;
using System.Text;
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

    private readonly byte[] bufferForNonHistogramMetrics = new byte[BufferSize];

    private readonly byte[] bufferForHistogramMetrics = new byte[BufferSize];

    private readonly int bufferIndexForNonHistogramMetrics;

    private readonly int bufferIndexForHistogramMetrics;

    private static readonly MetricData ulongZero = new MetricData { UInt64Value = 0 };

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
                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                            {
                                var ulongSum = Convert.ToUInt64(metricPoint.GetSumLong());
                                var metricData = new MetricData { UInt64Value = ulongSum };
                                var bodyLength = this.SerializeMetric(
                                    MetricEventType.ULongMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(), // Using the endTime here as the timestamp as Geneva Metrics only allows for one field for timestamp
                                    metricPoint.Tags,
                                    metricData);
                                this.metricDataTransport.Send(MetricEventType.ULongMetric, this.bufferForNonHistogramMetrics, bodyLength);
                                break;
                            }

                        case MetricType.LongGauge:
                            {
                                var ulongSum = Convert.ToUInt64(metricPoint.GetGaugeLastValueLong());
                                var metricData = new MetricData { UInt64Value = ulongSum };
                                var bodyLength = this.SerializeMetric(
                                    MetricEventType.ULongMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData);
                                this.metricDataTransport.Send(MetricEventType.ULongMetric, this.bufferForNonHistogramMetrics, bodyLength);
                                break;
                            }

                        case MetricType.DoubleSum:
                            {
                                var doubleSum = metricPoint.GetSumDouble();
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetric(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData);
                                this.metricDataTransport.Send(MetricEventType.DoubleMetric, this.bufferForNonHistogramMetrics, bodyLength);
                                break;
                            }

                        case MetricType.DoubleGauge:
                            {
                                var doubleSum = metricPoint.GetGaugeLastValueDouble();
                                var metricData = new MetricData { DoubleValue = doubleSum };
                                var bodyLength = this.SerializeMetric(
                                    MetricEventType.DoubleMetric,
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricData);
                                this.metricDataTransport.Send(MetricEventType.DoubleMetric, this.bufferForNonHistogramMetrics, bodyLength);
                                break;
                            }

                        case MetricType.Histogram:
                            {
                                var sum = new MetricData { UInt64Value = Convert.ToUInt64(metricPoint.GetHistogramSum()) };
                                var count = Convert.ToUInt32(metricPoint.GetHistogramCount());
                                var bodyLength = this.SerializeHistogramMetric(
                                    metric.Name,
                                    metricPoint.EndTime.ToFileTime(),
                                    metricPoint.Tags,
                                    metricPoint.GetHistogramBuckets(),
                                    sum,
                                    count);
                                this.metricDataTransport.Send(MetricEventType.ExternallyAggregatedULongDistributionMetric, this.bufferForHistogramMetrics, bodyLength);
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
        uint count)
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
                    if (bucket.ExplicitBound != double.PositiveInfinity)
                    {
                        MetricSerializer.SerializeUInt64(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt64(bucket.ExplicitBound));
                        lastExplicitBound = bucket.ExplicitBound;
                    }
                    else
                    {
                        // The bucket to catch the overflows is one greater than the last bound provided
                        MetricSerializer.SerializeUInt64(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt64(lastExplicitBound + 1));
                    }

                    MetricSerializer.SerializeUInt32(this.bufferForHistogramMetrics, ref bufferIndex, Convert.ToUInt32(bucket.BucketCount));
                    bucketCount++;
                }
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
                payloadPtr[0].Min = ulongZero;
                payloadPtr[0].Max = ulongZero;
            }
        }
        finally
        {
        }

        return bodyLength;
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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class OtlpProtobufMetricExporterTests
{
    public TagList TagList;

    public TagList TagListWithPrepopulatedDimensions;

    private static readonly Dictionary<string, object> prepopulatedMetricDimensions = new Dictionary<string, object>
    {
        { "Dim1", 1 },
        { "Dim2", 2 },
        { "Dim3", 3 },
    };

    public OtlpProtobufMetricExporterTests()
    {
        this.TagList = default;

        bool boolValue = true;
        double doubleValue = 23.45;
        int intValue = 29;
        long longValue = 345;
        double negativeDoubleValue = -23.45;
        int negativeIntValue = -29;
        long negativeLongValue = -97;
        sbyte negativeSbyteValue = sbyte.MinValue;
        short negativeShortValue = -12;
        sbyte sByteValue = sbyte.MaxValue;
        short shortValue = short.MaxValue;
        string stringValueAscii = "TestString";
        string stringValueMixAsciiAndUnicode = "\u0418TestString";
        string stringValueUnicode = "\u0418";
        uint uintValue = uint.MaxValue;
        ulong ulongValue = 1234;
        ushort ushortValue = ushort.MaxValue;

        // Keep the keys in sorted order, Sdk outputs them in sorted order.
        this.TagList.Add(new("boolKey", boolValue));
        this.TagList.Add(new("doubleKey", doubleValue));
        this.TagList.Add(new("intKey", intValue));
        this.TagList.Add(new("longKey", longValue));
        this.TagList.Add(new("negativeDoubleKey", negativeDoubleValue));
        this.TagList.Add(new("negativeIntKey", negativeIntValue));
        this.TagList.Add(new("negativeLongKey", negativeLongValue));
        this.TagList.Add(new("negativeSbyteKey", negativeSbyteValue));
        this.TagList.Add(new("negativeShortKey", negativeShortValue));
        this.TagList.Add(new("sByteKey", sByteValue));
        this.TagList.Add(new("shortKey", shortValue));
        this.TagList.Add(new("stringValueAsciiKey", stringValueAscii));
        this.TagList.Add(new("stringValueMixAsciiAndUnicodeKey", stringValueMixAsciiAndUnicode));
        this.TagList.Add(new("stringValueUnicodeKey", stringValueUnicode));
        this.TagList.Add(new("uintKey", uintValue));
        this.TagList.Add(new("ulongKey", ulongValue));
        this.TagList.Add(new("ushortKey", ushortValue));
    }

    [Theory]
    [InlineData("longcounter", 123L, null, true)]
    [InlineData("longcounter", 123L, null, false)]
    [InlineData("doublecounter", null, 123.45, true)]
    [InlineData("doublecounter", null, 123.45, false)]
    [InlineData("longcounter", -123L, null, true)]
    [InlineData("longcounter", -123L, null, false)]
    [InlineData("doublecounter", null, -123.45, true)]
    [InlineData("doublecounter", null, -123.45, false)]
    public void CounterSerializationSingleMetricPoint(string instrumentName, long? longValue, double? doubleValue, bool addPrepopulatedDimensions)
    {
        using var meter = new Meter(nameof(this.CounterSerializationSingleMetricPoint), "0.0.1");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.CounterSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
        .Build();

        if (longValue != null)
        {
            var counter = meter.CreateCounter<long>(instrumentName);
            counter.Add(longValue.Value, this.TagList);
        }
        else
        {
            var counter = meter.CreateCounter<double>(instrumentName);
            counter.Add(doubleValue.Value, this.TagList);
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, addPrepopulatedDimensions ? prepopulatedMetricDimensions : null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, meterProvider.GetResource(), new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Single(testTransport.ExportedItems);

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        request.MergeFrom(testTransport.ExportedItems[0]);

        Assert.Single(request.ResourceMetrics);

        Assert.NotNull(request.ResourceMetrics[0].Resource);

        AssertOtlpAttributes([new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue")], request.ResourceMetrics[0].Resource.Attributes);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

        var scope = request.ResourceMetrics[0].ScopeMetrics[0];

        Assert.Equal(meter.Name, scope.Scope.Name);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

        var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

        Assert.Equal(instrumentName, metric.Name);

        Assert.NotNull(metric.Sum);

        Assert.Equal(1, (int)metric.Sum.AggregationTemporality);

        Assert.True(metric.Sum.IsMonotonic);

        Assert.Single(metric.Sum.DataPoints);

        var dataPoint = metric.Sum.DataPoints[0];

        if (longValue != null)
        {
            Assert.Equal(longValue, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(doubleValue, dataPoint.AsDouble);
        }

        // Assert time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;

        Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

        Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

        if (addPrepopulatedDimensions)
        {
            AssertOtlpAttributes(this.TagList.Concat(prepopulatedMetricDimensions), dataPoint.Attributes);
        }
        else
        {
            AssertOtlpAttributes(this.TagList, dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData("longcounter", new long[] { 10, 20, 30 }, null)]
    [InlineData("longcounter", new long[] { -10, 2, -30 }, null)]
    [InlineData("doublecounter", null, new double[] { 10.20, 2, 30.65 })]
    [InlineData("doublecounter", null, new double[] { -10.20, 2, -30.65 })]
    public void CounterSerializationMultipleMetricPoints(string instrumentName, long[] longValues, double[] doubleValues)
    {
        using var meter = new Meter(nameof(this.CounterSerializationMultipleMetricPoints), "0.0.1");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(nameof(this.CounterSerializationMultipleMetricPoints))
            .AddReader(inMemoryReader)
            .Build();

        int expectedMetricPoints = longValues != null ? longValues.Length : doubleValues.Length;
        TagList[] tags = new TagList[expectedMetricPoints];

        for (int i = 0; i < tags.Length; i++)
        {
            for (int j = 1; j <= (i + 1); j++)
            {
                tags[i].Add(new("tag" + j, "value" + j));
            }
        }

        if (longValues != null)
        {
            var counter = meter.CreateCounter<long>(instrumentName);

            for (int i = 0; i < longValues.Length; i++)
            {
                counter.Add(longValues[i], tags[i]);
            }
        }
        else
        {
            var counter = meter.CreateCounter<double>(instrumentName);

            for (int i = 0; i < doubleValues.Length; i++)
            {
                counter.Add(doubleValues[i], tags[i]);
            }
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, Resource.Empty, new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Equal(expectedMetricPoints, testTransport.ExportedItems.Count);

        // For asserting time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();

        for (int i = 0; i < expectedMetricPoints; i++)
        {
            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.MergeFrom(testTransport.ExportedItems[i]);

            Assert.Single(request.ResourceMetrics);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

            var scope = request.ResourceMetrics[0].ScopeMetrics[0];

            Assert.Equal(meter.Name, scope.Scope.Name);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

            var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

            Assert.Equal(instrumentName, metric.Name);

            Assert.NotNull(metric.Sum);

            Assert.Equal(1, (int)metric.Sum.AggregationTemporality);

            Assert.True(metric.Sum.IsMonotonic);

            Assert.Single(metric.Sum.DataPoints);

            var dataPoint = metric.Sum.DataPoints[0];

            if (longValues != null)
            {
                Assert.Equal(longValues[i], dataPoint.AsInt);
            }
            else
            {
                Assert.Equal(doubleValues[i], dataPoint.AsDouble);
            }

            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;

            Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

            Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

            AssertOtlpAttributes(tags[i], dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData("updownlongcounter", 123L, null, true)]
    [InlineData("updownlongcounter", 123L, null, false)]
    [InlineData("updowndoublecounter", null, 123.45, true)]
    [InlineData("updowndoublecounter", null, 123.45, false)]
    [InlineData("updownlongcounter", -123L, null, true)]
    [InlineData("updownlongcounter", -123L, null, false)]
    [InlineData("updowndoublecounter", null, -123.45, true)]
    [InlineData("updowndoublecounter", null, -123.45, false)]
    public void UpdownCounterSerializationSingleMetricPoint(string instrumentName, long? longValue, double? doubleValue, bool addPrepopulatedDimensions)
    {
        using var meter = new Meter(nameof(this.UpdownCounterSerializationSingleMetricPoint), "0.0.1");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.UpdownCounterSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
        .Build();

        if (longValue != null)
        {
            var counter = meter.CreateUpDownCounter<long>(instrumentName);
            counter.Add(longValue.Value, this.TagList);
        }
        else
        {
            var counter = meter.CreateUpDownCounter<double>(instrumentName);
            counter.Add(doubleValue.Value, this.TagList);
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, addPrepopulatedDimensions ? prepopulatedMetricDimensions : null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, meterProvider.GetResource(), new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Single(testTransport.ExportedItems);

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        request.MergeFrom(testTransport.ExportedItems[0]);

        Assert.Single(request.ResourceMetrics);

        Assert.NotNull(request.ResourceMetrics[0].Resource);

        AssertOtlpAttributes([new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue")], request.ResourceMetrics[0].Resource.Attributes);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

        var scope = request.ResourceMetrics[0].ScopeMetrics[0];

        Assert.Equal(meter.Name, scope.Scope.Name);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

        var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

        Assert.Equal(instrumentName, metric.Name);

        Assert.NotNull(metric.Sum);

        Assert.Equal(2, (int)metric.Sum.AggregationTemporality);

        Assert.False(metric.Sum.IsMonotonic);

        Assert.Single(metric.Sum.DataPoints);

        var dataPoint = metric.Sum.DataPoints[0];

        if (longValue != null)
        {
            Assert.Equal(longValue, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(doubleValue, dataPoint.AsDouble);
        }

        // Assert time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;

        Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

        Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

        if (addPrepopulatedDimensions)
        {
            AssertOtlpAttributes(this.TagList.Concat(prepopulatedMetricDimensions), dataPoint.Attributes);
        }
        else
        {
            AssertOtlpAttributes(this.TagList, dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData("updownlongcounter", new long[] { 10, 20, 30 }, null)]
    [InlineData("updownlongcounter", new long[] { 10, -2, 30 }, null)]
    [InlineData("updowndoublecounter", null, new double[] { 10.20, 2, 30.65 })]
    [InlineData("updowndoublecounter", null, new double[] { -10.20, 2, -30.65 })]
    public void UpdownCounterSerializationMultipleMetricPoints(string instrumentName, long[] longValues, double[] doubleValues)
    {
        using var meter = new Meter(nameof(this.UpdownCounterSerializationMultipleMetricPoints), "0.0.1");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(nameof(this.UpdownCounterSerializationMultipleMetricPoints))
            .AddReader(inMemoryReader)
            .Build();

        int expectedMetricPoints = longValues != null ? longValues.Length : doubleValues.Length;
        TagList[] tags = new TagList[expectedMetricPoints];

        for (int i = 0; i < tags.Length; i++)
        {
            for (int j = 1; j <= (i + 1); j++)
            {
                tags[i].Add(new("tag" + j, "value" + j));
            }
        }

        if (longValues != null)
        {
            var counter = meter.CreateUpDownCounter<long>(instrumentName);

            for (int i = 0; i < longValues.Length; i++)
            {
                counter.Add(longValues[i], tags[i]);
            }
        }
        else
        {
            var counter = meter.CreateUpDownCounter<double>(instrumentName);

            for (int i = 0; i < doubleValues.Length; i++)
            {
                counter.Add(doubleValues[i], tags[i]);
            }
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, Resource.Empty, new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Equal(expectedMetricPoints, testTransport.ExportedItems.Count);

        // For asserting time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();

        for (int i = 0; i < expectedMetricPoints; i++)
        {
            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.MergeFrom(testTransport.ExportedItems[i]);

            Assert.Single(request.ResourceMetrics);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

            var scope = request.ResourceMetrics[0].ScopeMetrics[0];

            Assert.Equal(meter.Name, scope.Scope.Name);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

            var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

            Assert.Equal(instrumentName, metric.Name);

            Assert.NotNull(metric.Sum);

            Assert.Equal(2, (int)metric.Sum.AggregationTemporality);

            Assert.False(metric.Sum.IsMonotonic);

            Assert.Single(metric.Sum.DataPoints);

            var dataPoint = metric.Sum.DataPoints[0];

            if (longValues != null)
            {
                Assert.Equal(longValues[i], dataPoint.AsInt);
            }
            else
            {
                Assert.Equal(doubleValues[i], dataPoint.AsDouble);
            }

            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;

            Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

            Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

            AssertOtlpAttributes(tags[i], dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData(123.45, true)]
    [InlineData(123.45, false)]
    [InlineData(-123.45, true)]
    [InlineData(-123.45, false)]
    public void HistogramSerializationSingleMetricPoint(double doubleValue, bool addPrepopulatedDimensions)
    {
        using var meter = new Meter(nameof(this.HistogramSerializationSingleMetricPoint), "0.0.1");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.HistogramSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
        .Build();

        var histogram = meter.CreateHistogram<double>("TestHistogram");
        histogram.Record(doubleValue, this.TagList);

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, addPrepopulatedDimensions ? prepopulatedMetricDimensions : null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, meterProvider.GetResource(), new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Single(testTransport.ExportedItems);

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        request.MergeFrom(testTransport.ExportedItems[0]);

        Assert.Single(request.ResourceMetrics);

        Assert.NotNull(request.ResourceMetrics[0].Resource);

        AssertOtlpAttributes([new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue")], request.ResourceMetrics[0].Resource.Attributes);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

        var scope = request.ResourceMetrics[0].ScopeMetrics[0];

        Assert.Equal(meter.Name, scope.Scope.Name);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

        var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

        Assert.Equal("TestHistogram", metric.Name);

        Assert.NotNull(metric.Histogram);

        Assert.Equal(1, (int)metric.Histogram.AggregationTemporality);

        Assert.Single(metric.Histogram.DataPoints);

        var dataPoint = metric.Histogram.DataPoints[0];

        Assert.Equal(doubleValue, dataPoint.Sum);

        Assert.Equal(doubleValue, dataPoint.Max);

        Assert.Equal(doubleValue, dataPoint.Min);

        // Assert time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;

        int bucketCountIndex = 0;
        int explicitBoundCountIndex = 0;

        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
        {
            var bucketCount = (ulong)histogramMeasurement.BucketCount;

            Assert.Equal(bucketCount, dataPoint.BucketCounts[bucketCountIndex]);

            if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
            {
                Assert.Equal(histogramMeasurement.ExplicitBound, dataPoint.ExplicitBounds[explicitBoundCountIndex]);
                explicitBoundCountIndex++;
            }

            bucketCountIndex++;
        }

        Assert.Equal(bucketCountIndex, dataPoint.BucketCounts.Count);

        Assert.Equal(explicitBoundCountIndex, dataPoint.ExplicitBounds.Count);

        Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

        Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

        if (addPrepopulatedDimensions)
        {
            AssertOtlpAttributes(this.TagList.Concat(prepopulatedMetricDimensions), dataPoint.Attributes);
        }
        else
        {
            AssertOtlpAttributes(this.TagList, dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData(new[] { -123.45, 23, .05, 100 })]
    public void HistogramSerializationMultipleMetricPoints(double[] doubleValues)
    {
        using var meter = new Meter(nameof(this.HistogramSerializationSingleMetricPoint), "0.0.1");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.HistogramSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
            .Build();

        var histogram = meter.CreateHistogram<double>("TestHistogram");

        int expectedMetricPointCount = doubleValues.Length;

        TagList[] tags = new TagList[expectedMetricPointCount];

        for (int i = 0; i < tags.Length; i++)
        {
            for (int j = 1; j <= (i + 1); j++)
            {
                tags[i].Add(new("tag" + j, "value" + j));
            }
        }

        for (int i = 0; i < expectedMetricPointCount; i++)
        {
            histogram.Record(doubleValues[i], tags[i]);
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, meterProvider.GetResource(), new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Equal(expectedMetricPointCount, testTransport.ExportedItems.Count);

        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();

        for (int i = 0; i < expectedMetricPointCount; i++)
        {
            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.MergeFrom(testTransport.ExportedItems[i]);

            Assert.NotNull(request.ResourceMetrics[0].Resource);

            AssertOtlpAttributes([new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue")], request.ResourceMetrics[0].Resource.Attributes);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

            var scope = request.ResourceMetrics[0].ScopeMetrics[0];

            Assert.Equal(meter.Name, scope.Scope.Name);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

            var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

            Assert.Equal("TestHistogram", metric.Name);

            Assert.NotNull(metric.Histogram);

            Assert.Equal(1, (int)metric.Histogram.AggregationTemporality);

            Assert.Single(metric.Histogram.DataPoints);

            var dataPoint = metric.Histogram.DataPoints[0];

            Assert.Equal(doubleValues[i], dataPoint.Sum);

            Assert.Equal(doubleValues[i], dataPoint.Max);

            Assert.Equal(doubleValues[i], dataPoint.Min);

            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;

            int bucketCountIndex = 0;
            int explicitBoundCountIndex = 0;

            foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
            {
                var bucketCount = (ulong)histogramMeasurement.BucketCount;

                Assert.Equal(bucketCount, dataPoint.BucketCounts[bucketCountIndex]);

                if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                {
                    Assert.Equal(histogramMeasurement.ExplicitBound, dataPoint.ExplicitBounds[explicitBoundCountIndex]);
                    explicitBoundCountIndex++;
                }

                bucketCountIndex++;
            }

            Assert.Equal(bucketCountIndex, dataPoint.BucketCounts.Count);

            Assert.Equal(explicitBoundCountIndex, dataPoint.ExplicitBounds.Count);

            Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

            Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

            AssertOtlpAttributes(tags[i], dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData("longGauge", 123L, null, true)]
    [InlineData("longGauge", 123L, null, false)]
    [InlineData("doubleGauge", null, 123.45, true)]
    [InlineData("doubleGauge", null, 123.45, false)]
    [InlineData("longGauge", -123L, null, true)]
    [InlineData("longGauge", -123L, null, false)]
    [InlineData("doubleGauge", null, -123.45, true)]
    [InlineData("doubleGauge", null, -123.45, false)]
    public void GaugeSerializationSingleMetricPoint(string instrumentName, long? longValue, double? doubleValue, bool addPrepopulatedDimensions)
    {
        using var meter = new Meter(nameof(this.GaugeSerializationSingleMetricPoint), "0.0.1");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.GaugeSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
            .Build();

        if (longValue.HasValue)
        {
            meter.CreateObservableGauge(
                instrumentName,
                () => new List<Measurement<long>>()
                {
                    new(longValue.Value, this.TagList),
                });
        }
        else
        {
            meter.CreateObservableGauge(
                instrumentName,
                () => new List<Measurement<double>>()
                {
                new(doubleValue.Value, this.TagList),
                });
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, addPrepopulatedDimensions ? prepopulatedMetricDimensions : null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, meterProvider.GetResource(), new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Single(testTransport.ExportedItems);

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        request.MergeFrom(testTransport.ExportedItems[0]);

        Assert.Single(request.ResourceMetrics);

        Assert.NotNull(request.ResourceMetrics[0].Resource);

        AssertOtlpAttributes([new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue")], request.ResourceMetrics[0].Resource.Attributes);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

        var scope = request.ResourceMetrics[0].ScopeMetrics[0];

        Assert.Equal(meter.Name, scope.Scope.Name);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

        var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

        Assert.Equal(instrumentName, metric.Name);

        Assert.NotNull(metric.Gauge);

        Assert.Single(metric.Gauge.DataPoints);

        var dataPoint = metric.Gauge.DataPoints[0];

        if (longValue.HasValue)
        {
            Assert.Equal(longValue.Value, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(doubleValue.Value, dataPoint.AsDouble);
        }

        // Assert time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;

        Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

        Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

        if (addPrepopulatedDimensions)
        {
            AssertOtlpAttributes(this.TagList.Concat(prepopulatedMetricDimensions), dataPoint.Attributes);
        }
        else
        {
            AssertOtlpAttributes(this.TagList, dataPoint.Attributes);
        }
    }

    [Theory]
    [InlineData("longGauge", new long[] { 10, 20, 30 }, null)]
    [InlineData("longGauge", new long[] { -10, 2, -30 }, null)]
    [InlineData("doubleGauge", null, new double[] { 10.20, 2, 30.65 })]
    [InlineData("doubleGauge", null, new double[] { -10.20, 2, -30.65 })]
    public void GaugeSerializationMultipleMetricPoints(string instrumentName, long[] longValues, double[] doubleValues)
    {
        using var meter = new Meter(nameof(this.GaugeSerializationMultipleMetricPoints), "0.0.1");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(nameof(this.GaugeSerializationMultipleMetricPoints))
            .AddReader(inMemoryReader)
            .Build();

        int expectedMetricPoints = longValues != null ? longValues.Length : doubleValues.Length;
        TagList[] tags = new TagList[expectedMetricPoints];

        for (int i = 0; i < tags.Length; i++)
        {
            for (int j = 1; j <= (i + 1); j++)
            {
                tags[i].Add(new("tag" + j, "value" + j));
            }
        }

        if (longValues != null)
        {
            var counter = meter.CreateCounter<long>(instrumentName);

            meter.CreateObservableGauge(
                instrumentName,
                () =>
                {
                    List<Measurement<long>> list = new List<Measurement<long>>();
                    for (int i = 0; i < longValues.Length; i++)
                    {
                        list.Add(new(longValues[i], tags[i]));
                    }

                    return list;
                });
        }
        else
        {
            meter.CreateObservableGauge(
               instrumentName,
               () =>
               {
                   List<Measurement<double>> list = new List<Measurement<double>>();
                   for (int i = 0; i < doubleValues.Length; i++)
                   {
                       list.Add(new(doubleValues[i], tags[i]));
                   }

                   return list;
               });
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport, null, null);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, Resource.Empty, new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        Assert.Equal(expectedMetricPoints, testTransport.ExportedItems.Count);

        // For asserting time
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();

        for (int i = 0; i < expectedMetricPoints; i++)
        {
            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.MergeFrom(testTransport.ExportedItems[i]);

            Assert.Single(request.ResourceMetrics);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

            var scope = request.ResourceMetrics[0].ScopeMetrics[0];

            Assert.Equal(meter.Name, scope.Scope.Name);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

            var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

            Assert.Equal(instrumentName, metric.Name);

            Assert.NotNull(metric.Gauge);

            Assert.Single(metric.Gauge.DataPoints);

            var dataPoint = metric.Gauge.DataPoints[0];

            if (longValues != null)
            {
                Assert.Equal(longValues[i], dataPoint.AsInt);
            }
            else
            {
                Assert.Equal(doubleValues[i], dataPoint.AsDouble);
            }

            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;

            Assert.Equal((ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(), dataPoint.StartTimeUnixNano);

            Assert.Equal((ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(), dataPoint.TimeUnixNano);

            AssertOtlpAttributes(tags[i], dataPoint.Attributes);
        }
    }

    internal static void AssertOtlpAttributes(
        IEnumerable<KeyValuePair<string, object>> expected,
        RepeatedField<OtlpCommon.KeyValue> actual)
    {
        var expectedAttributes = expected.ToList();
        int expectedSize = 0;
        int expectedAttributesCount = expectedAttributes.Count;
        for (int i = 0; i < expectedAttributesCount; i++)
        {
            var current = expectedAttributes[i].Value;

            Assert.Equal(expectedAttributes[i].Key, actual[i].Key);
            Assert.Equal(expectedAttributes[i].Key, actual[i].Key);
            AssertOtlpAttributeValue(current, actual[i].Value);
            expectedSize++;
        }

        Assert.Equal(expectedSize, actual.Count);
    }

    private static void AssertOtlpAttributeValue(object expected, OtlpCommon.AnyValue actual)
    {
        switch (expected)
        {
            case string s:
                Assert.Equal(s, actual.StringValue);
                break;
            case bool b:
                Assert.Equal(b, actual.BoolValue);
                break;
            case long l:
                Assert.Equal(l, actual.IntValue);
                break;
            case double d:
                Assert.Equal(d, actual.DoubleValue);
                break;
            case int i:
                Assert.Equal(i, actual.IntValue);
                break;
            case uint u:
                Assert.Equal(u, actual.IntValue);
                break;
            case ushort us:
                Assert.Equal(us, actual.IntValue);
                break;
            case short s:
                Assert.Equal(s, actual.IntValue);
                break;
            case ulong ul:
                Assert.Equal(ul, (ulong)actual.IntValue);
                break;
            case sbyte sb:
                Assert.Equal(sb, actual.IntValue);
                break;
            default:
                Assert.Equal(expected.ToString(), actual.StringValue);
                break;
        }
    }

    private class TestTransport : IMetricDataTransport
    {
        public List<byte[]> ExportedItems = new();

        public void SendOtlpProtobufEvent(byte[] body, int size)
        {
            var arr = new byte[size];
            Buffer.BlockCopy(body, 0, arr, 0, arr.Length);
            this.ExportedItems.Add(arr);
        }

        public void Send(MetricEventType eventType, byte[] body, int size)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}

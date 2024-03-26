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
    [Theory]
    [InlineData("longcounter", 123L, null)]
    [InlineData("doublecounter", null, 123.45)]
    [InlineData("longcounter", -123L, null)]
    [InlineData("doublecounter", null, -123.45)]
    public void CounterSerializationSingleMetricPoint(string instrumentName, long? longValue, double? doubleValue)
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
            counter.Add(longValue.Value, new("tag1", "value1"), new("tag2", "value2"));
        }
        else
        {
            var counter = meter.CreateCounter<double>(instrumentName);
            counter.Add(doubleValue.Value, new("tag1", "value1"), new("tag2", "value2"));
        }

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport);

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

        AssertOtlpAttributes([new("tag1", "value1"), new("tag2", "value2")], dataPoint.Attributes);
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
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport);

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

            // This is a side effect of writing data in buffer from end to beginning
            // Elements are in reverse order.
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

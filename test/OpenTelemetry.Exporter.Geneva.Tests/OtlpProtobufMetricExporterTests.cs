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
    [InlineData(123)]
    [InlineData(-123)]
    public void LongCounterSerializationSingleMetricPoint(long value)
    {
        using var meter = new Meter(nameof(this.LongCounterSerializationSingleMetricPoint), "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var resourceBuilder = ResourceBuilder.CreateDefault().Clear()
            .AddAttributes(new[] { new KeyValuePair<string, object>("TestResourceKey", "TestResourceValue") });
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(this.LongCounterSerializationSingleMetricPoint))
            .AddReader(inMemoryReader)
            .Build();

        longCounter.Add(value, new("tag1", "value1"), new("tag2", "value2"));

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

        Assert.Equal(longCounter.Name, metric.Name);

        Assert.NotNull(metric.Sum);

        Assert.Equal(1, (int)metric.Sum.AggregationTemporality);

        Assert.True(metric.Sum.IsMonotonic);

        Assert.Single(metric.Sum.DataPoints);

        var dataPoint = metric.Sum.DataPoints[0];

        Assert.Equal(value, dataPoint.AsInt);

        AssertOtlpAttributes([new("tag1", "value1"), new("tag2", "value2")], dataPoint.Attributes);
    }

    [Fact]
    public void LongCounterSerializationMultipleMetricPoints()
    {
        using var meter = new Meter(nameof(this.LongCounterSerializationMultipleMetricPoints), "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(nameof(this.LongCounterSerializationMultipleMetricPoints))
            .AddReader(inMemoryReader)
            .Build();

        TagList[] tags = new TagList[3];

        tags[0].Add(new("tag1", "value1"));
        tags[0].Add(new("tag2", "value2"));

        tags[1].Add(new("tag1", "value1"));
        tags[1].Add(new("tag2", "value2"));
        tags[1].Add(new("tag3", "value3"));

        tags[2].Add(new("tag1", "value1"));
        tags[2].Add(new("tag2", "value2"));
        tags[2].Add(new("tag3", "value3"));
        tags[2].Add(new("tag4", "value4"));

        longCounter.Add(62, tags[0]);

        longCounter.Add(62, tags[0]);

        longCounter.Add(124, tags[1]);

        longCounter.Add(124, tags[2]);

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        var testTransport = new TestTransport();
        var otlpProtobufSerializer = new OtlpProtobufSerializer(testTransport);

        otlpProtobufSerializer.SerializeAndSendMetrics(buffer, Resource.Empty, new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        // 3 unique measurements.
        var exportedItemsCount = testTransport.ExportedItems.Count;
        Assert.Equal(3, exportedItemsCount);

        for (int i = 0; i < exportedItemsCount; i++)
        {
            var request = new OtlpCollector.ExportMetricsServiceRequest();

            request.MergeFrom(testTransport.ExportedItems[i]);

            Assert.Single(request.ResourceMetrics);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

            var scope = request.ResourceMetrics[0].ScopeMetrics[0];

            Assert.Equal(meter.Name, scope.Scope.Name);

            Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

            var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

            Assert.Equal(longCounter.Name, metric.Name);

            Assert.NotNull(metric.Sum);

            Assert.Equal(1, (int)metric.Sum.AggregationTemporality);

            Assert.True(metric.Sum.IsMonotonic);

            Assert.Single(metric.Sum.DataPoints);

            var dataPoint = metric.Sum.DataPoints[0];

            Assert.Equal(124, dataPoint.AsInt);

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

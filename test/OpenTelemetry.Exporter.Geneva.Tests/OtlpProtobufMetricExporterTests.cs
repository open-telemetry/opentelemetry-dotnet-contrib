// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class OtlpProtobufMetricExporterTests
{
    [Fact]
    public void LongCounterSerialization()
    {
        using var meter = new Meter("LongCounterSerializationOtlpProtobuf", "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("LongCounterSerializationOtlpProtobuf")
            .AddReader(inMemoryReader)
            .Build();

        longCounter.Add(123, new("tag1", "value1"), new("tag2", "value2"));

        meterProvider.ForceFlush();

        var buffer = new byte[65360];

        int currentPosition = 0;

        var otlpProtobufSerializer = new OtlpProtobufSerializer();

        otlpProtobufSerializer.SerializeMetrics(buffer, ref currentPosition, null, new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count));

        byte[] arr = new byte[currentPosition];

        Buffer.BlockCopy(buffer, 0, arr, 0, arr.Length);

        var request = new OtlpCollector.ExportMetricsServiceRequest();

        request.MergeFrom(arr);

        Assert.Single(request.ResourceMetrics);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics);

        var scope = request.ResourceMetrics[0].ScopeMetrics[0];

        Assert.Equal(meter.Name, scope.Scope.Name);

        Assert.Single(request.ResourceMetrics[0].ScopeMetrics[0].Metrics);

        var metric = request.ResourceMetrics[0].ScopeMetrics[0].Metrics[0];

        Assert.Equal(longCounter.Name, metric.Name);

        Assert.NotNull(metric.Sum);

        Assert.Single(metric.Sum.DataPoints);

        var dataPoint = metric.Sum.DataPoints[0];

        Assert.Equal(123, dataPoint.AsInt);

        AssertOtlpAttributes([new("tag1", "value1"), new("tag2", "value2")], dataPoint.Attributes);
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
}

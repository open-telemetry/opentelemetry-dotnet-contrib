// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.Geneva.Metrics;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method                                                   | Mean      | Error     | StdDev    | Allocated |
|--------------------------------------------------------- |----------:|----------:|----------:|----------:|
| InstrumentWithNoListener3Dimensions                      |  23.83 ns |  0.080 ns |  0.075 ns |         - |
| InstrumentWithNoListener4Dimensions                      |  56.71 ns |  0.730 ns |  0.647 ns |         - |
| InstrumentWithWithListener3Dimensions                    |  24.46 ns |  0.257 ns |  0.215 ns |         - |
| InstrumentWithWithListener4Dimensions                    |  59.02 ns |  0.501 ns |  0.444 ns |         - |
| InstrumentWithWithDummyReader3Dimensions                 | 139.70 ns |  1.189 ns |  1.113 ns |         - |
| InstrumentWithWithDummyReader4Dimensions                 | 184.76 ns |  2.012 ns |  1.680 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter3Dimensions | 135.76 ns |  0.995 ns |  0.831 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter4Dimensions | 193.14 ns |  1.507 ns |  1.336 ns |         - |
| SerializeCounterMetricItemWith3Dimensions                | 165.27 ns |  1.000 ns |  0.887 ns |         - |
| SerializeCounterMetricItemWith4Dimensions                | 191.13 ns |  1.201 ns |  1.003 ns |         - |
| ExportCounterMetricItemWith3Dimensions                   | 657.63 ns | 13.019 ns | 13.369 ns |         - |
| ExportCounterMetricItemWith4Dimensions                   | 682.76 ns | 13.598 ns | 32.318 ns |         - |
| SerializeHistogramMetricItemWith3Dimensions              | 285.39 ns |  4.195 ns |  3.503 ns |         - |
| SerializeHistogramMetricItemWith4Dimensions              | 303.15 ns |  3.834 ns |  3.587 ns |         - |
| ExportHistogramMetricItemWith3Dimensions                 | 803.90 ns | 14.753 ns | 36.465 ns |         - |
| ExportHistogramMetricItemWith4Dimensions                 | 810.62 ns | 14.445 ns | 11.278 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class MetricExporterBenchmarks
{
    private Metric counterMetricWith3Dimensions;
    private Metric counterMetricWith4Dimensions;
    private MetricPoint counterMetricPointWith3Dimensions;
    private MetricPoint counterMetricPointWith4Dimensions;
    private MetricData counterMetricDataWith3Dimensions;
    private MetricData counterMetricDataWith4Dimensions;
    private Batch<Metric> counterMetricBatchWith3Dimensions;
    private Batch<Metric> counterMetricBatchWith4Dimensions;
    private Metric histogramMetricWith3Dimensions;
    private Metric histogramMetricWith4Dimensions;
    private MetricPoint histogramMetricPointWith3Dimensions;
    private MetricPoint histogramMetricPointWith4Dimensions;
    private ulong histogramSumWith3Dimensions;
    private ulong histogramSumWith4Dimensions;
    private double histogramMinWith3Dimensions;
    private double histogramMinWith4Dimensions;
    private double histogramMaxWith3Dimensions;
    private double histogramMaxWith4Dimensions;
    private uint histogramCountWith3Dimensions;
    private uint histogramCountWith4Dimensions;
    private Batch<Metric> histogramMetricBatchWith3Dimensions;
    private Batch<Metric> histogramMetricBatchWith4Dimensions;
    private Meter meterWithNoListener = new Meter("MeterWithNoListener", "0.0.1");
    private Meter meterWithListener = new Meter("MeterWithListener", "0.0.1");
    private Meter meterWithDummyReader = new Meter("MeterWithDummyReader", "0.0.1");
    private Meter meterWithGenevaMetricExporter = new Meter("MeterWithGenevaMetricExporter", "0.0.1");
    private Counter<long> counterWithNoListener;
    private Counter<long> counterWithListener;
    private Counter<long> counterWithDummyReader;
    private Counter<long> counterWithGenevaMetricExporter;
    private MeterListener listener;
    private MeterProvider meterProviderWithDummyReader;
    private MeterProvider meterProviderWithGenevaMetricExporter;
    private MeterProvider meterProviderForCounterBatchWith3Dimensions;
    private MeterProvider meterProviderForCounterBatchWith4Dimensions;
    private MeterProvider meterProviderForHistogramBatchWith3Dimensions;
    private MeterProvider meterProviderForHistogramBatchWith4Dimensions;
    private TlvMetricExporter tlvMetricsExporter;
    private ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

    private static readonly Random randomForHistogram = new Random(); // Use the same seed for all the benchmarks to have the same data exported
    private static readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

    [GlobalSetup]
    public void Setup()
    {
        this.counterWithNoListener = this.meterWithNoListener.CreateCounter<long>("counter");
        this.counterWithListener = this.meterWithListener.CreateCounter<long>("counter");
        this.counterWithDummyReader = this.meterWithDummyReader.CreateCounter<long>("counter");
        this.counterWithGenevaMetricExporter = this.meterWithGenevaMetricExporter.CreateCounter<long>("counter");

        var exporterOptions = new GenevaMetricExporterOptions() { ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace" };
        this.tlvMetricsExporter = new GenevaMetricExporter(exporterOptions).Exporter;

        this.counterMetricPointWith3Dimensions = this.GenerateCounterMetricItemWith3Dimensions(out this.counterMetricDataWith3Dimensions);
        this.counterMetricPointWith4Dimensions = this.GenerateCounterMetricItemWith4Dimensions(out this.counterMetricDataWith4Dimensions);

        this.counterMetricBatchWith3Dimensions = this.GenerateCounterBatchWith3Dimensions();
        this.counterMetricBatchWith4Dimensions = this.GenerateCounterBatchWith4Dimensions();

        using var enumeratorForCounterBatchWith3Dimensions = this.counterMetricBatchWith3Dimensions.GetEnumerator();
        enumeratorForCounterBatchWith3Dimensions.MoveNext();
        this.counterMetricWith3Dimensions = enumeratorForCounterBatchWith3Dimensions.Current;

        using var enumeratorForCounterBatchWith4Dimensions = this.counterMetricBatchWith4Dimensions.GetEnumerator();
        enumeratorForCounterBatchWith4Dimensions.MoveNext();
        this.counterMetricWith4Dimensions = enumeratorForCounterBatchWith4Dimensions.Current;

        this.histogramMetricPointWith3Dimensions = this.GenerateHistogramMetricItemWith3Dimensions(out this.histogramSumWith3Dimensions, out this.histogramCountWith3Dimensions, out this.histogramMinWith3Dimensions, out this.histogramMaxWith3Dimensions);
        this.histogramMetricPointWith4Dimensions = this.GenerateHistogramMetricItemWith4Dimensions(out this.histogramSumWith4Dimensions, out this.histogramCountWith4Dimensions, out this.histogramMinWith4Dimensions, out this.histogramMaxWith4Dimensions);

        this.histogramMetricBatchWith3Dimensions = this.GenerateHistogramBatchWith3Dimensions();
        this.histogramMetricBatchWith4Dimensions = this.GenerateHistogramBatchWith4Dimensions();

        using var enumeratorForHistogramBatchWith3Dimensions = this.histogramMetricBatchWith3Dimensions.GetEnumerator();
        enumeratorForHistogramBatchWith3Dimensions.MoveNext();
        this.histogramMetricWith3Dimensions = enumeratorForHistogramBatchWith3Dimensions.Current;

        using var enumeratorForHistogramBatchWith4Dimensions = this.histogramMetricBatchWith4Dimensions.GetEnumerator();
        enumeratorForHistogramBatchWith4Dimensions.MoveNext();
        this.histogramMetricWith4Dimensions = enumeratorForHistogramBatchWith4Dimensions.Current;

        #region Setup MeterListener
        this.listener = new MeterListener();
        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == this.meterWithListener.Name)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        this.listener.Start();
        #endregion

        this.meterProviderWithDummyReader = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meterWithDummyReader.Name)
            .AddReader(new DummyReader(new DummyMetricExporter()))
            .Build();

        this.meterProviderWithGenevaMetricExporter = Sdk.CreateMeterProviderBuilder()
            .AddMeter(this.meterWithGenevaMetricExporter.Name)
            .AddGenevaMetricExporter(options =>
            {
                options.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
            })
            .Build();
    }

    private MetricPoint GenerateCounterMetricItemWith3Dimensions(out MetricData metricData)
    {
        using var meterWithInMemoryExporter = new Meter("GenerateCounterMetricItemWith3Dimensions", "0.0.1");
        var counter = meterWithInMemoryExporter.CreateCounter<long>("CounterWithThreeDimensions");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateCounterMetricItemWith3Dimensions")
            .AddReader(inMemoryReader)
            .Build();

        counter.Add(
            100,
            new("DimName1", dimensionValues[this.random.Value.Next(0, 10)]),
            new("DimName2", dimensionValues[this.random.Value.Next(0, 10)]),
            new("DimName3", dimensionValues[this.random.Value.Next(0, 10)]));

        inMemoryReader.Collect();

        var metric = exportedItems[0];
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
        metricData = new MetricData { UInt64Value = metricDataValue };

        return metricPoint;
    }

    private MetricPoint GenerateCounterMetricItemWith4Dimensions(out MetricData metricData)
    {
        using var meterWithInMemoryExporter = new Meter("GenerateCounterMetricItemWith4Dimensions", "0.0.1");
        var counter = meterWithInMemoryExporter.CreateCounter<long>("CounterWith4Dimensions");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateCounterMetricItemWith4Dimensions")
            .AddReader(inMemoryReader)
            .Build();

        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        counter.Add(100, tags);

        inMemoryReader.Collect();

        var metric = exportedItems[0];
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
        metricData = new MetricData { UInt64Value = metricDataValue };

        return metricPoint;
    }

    private Batch<Metric> GenerateCounterBatchWith3Dimensions()
    {
        using var meterWithInMemoryExporter = new Meter("GenerateCounterBatchWith3Dimensions", "0.0.1");
        var counter = meterWithInMemoryExporter.CreateCounter<long>("CounterWithThreeDimensions");

        var batchGeneratorExporter = new BatchGenerator();
        var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        this.meterProviderForCounterBatchWith3Dimensions = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateCounterBatchWith3Dimensions")
            .AddReader(batchGeneratorReader)
            .Build();

        counter.Add(
            100,
            new("DimName1", dimensionValues[this.random.Value.Next(0, 10)]),
            new("DimName2", dimensionValues[this.random.Value.Next(0, 10)]),
            new("DimName3", dimensionValues[this.random.Value.Next(0, 10)]));

        this.meterProviderForCounterBatchWith3Dimensions.ForceFlush();
        return batchGeneratorExporter.Batch;
    }

    private Batch<Metric> GenerateCounterBatchWith4Dimensions()
    {
        using var meterWithInMemoryExporter = new Meter("GenerateCounterBatchWith4Dimensions", "0.0.1");
        var counter = meterWithInMemoryExporter.CreateCounter<long>("CounterWith4Dimensions");

        var batchGeneratorExporter = new BatchGenerator();
        var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        this.meterProviderForCounterBatchWith4Dimensions = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateCounterBatchWith4Dimensions")
            .AddReader(batchGeneratorReader)
            .Build();

        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        counter.Add(100, tags);

        this.meterProviderForCounterBatchWith4Dimensions.ForceFlush();
        return batchGeneratorExporter.Batch;
    }

    private MetricPoint GenerateHistogramMetricItemWith3Dimensions(out ulong sum, out uint count, out double min, out double max)
    {
        using var meterWithInMemoryExporter = new Meter("GenerateHistogramMetricItemWith3Dimensions", "0.0.1");
        var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith3Dimensions");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateHistogramMetricItemWith3Dimensions")
            .AddReader(inMemoryReader)
            .Build();

        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);

        for (int i = 0; i < 1000; i++)
        {
            histogram.Record(randomForHistogram.Next(1, 1000), tag1, tag2, tag3);
        }

        inMemoryReader.Collect();

        var metric = exportedItems[0];
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        sum = Convert.ToUInt64(metricPoint.GetHistogramSum());
        count = Convert.ToUInt32(metricPoint.GetHistogramCount());

        if (!metricPoint.TryGetHistogramMinMaxValues(out min, out max))
        {
            min = 0;
            max = 0;
        }

        return metricPoint;
    }

    private MetricPoint GenerateHistogramMetricItemWith4Dimensions(out ulong sum, out uint count, out double min, out double max)
    {
        using var meterWithInMemoryExporter = new Meter("GenerateHistogramMetricItemWith4Dimensions", "0.0.1");
        var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith4Dimensions");

        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateHistogramMetricItemWith4Dimensions")
            .AddReader(inMemoryReader)
            .Build();

        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        for (int i = 0; i < 1000; i++)
        {
            histogram.Record(randomForHistogram.Next(1, 1000), tags);
        }

        inMemoryReader.Collect();

        var metric = exportedItems[0];
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        sum = Convert.ToUInt64(metricPoint.GetHistogramSum());
        count = Convert.ToUInt32(metricPoint.GetHistogramCount());

        if (!metricPoint.TryGetHistogramMinMaxValues(out min, out max))
        {
            min = 0;
            max = 0;
        }

        return metricPoint;
    }

    private Batch<Metric> GenerateHistogramBatchWith3Dimensions()
    {
        using var meterWithInMemoryExporter = new Meter("GenerateHistogramBatchWith3Dimensions", "0.0.1");
        var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith3Dimensions");

        var batchGeneratorExporter = new BatchGenerator();
        var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        this.meterProviderForHistogramBatchWith3Dimensions = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateHistogramBatchWith3Dimensions")
            .AddReader(batchGeneratorReader)
            .Build();

        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);

        for (int i = 0; i < 1000; i++)
        {
            histogram.Record(randomForHistogram.Next(1, 1000), tag1, tag2, tag3);
        }

        this.meterProviderForHistogramBatchWith3Dimensions.ForceFlush();
        return batchGeneratorExporter.Batch;
    }

    private Batch<Metric> GenerateHistogramBatchWith4Dimensions()
    {
        using var meterWithInMemoryExporter = new Meter("GenerateHistogramBatchWith4Dimensions", "0.0.1");
        var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith4Dimensions");

        var batchGeneratorExporter = new BatchGenerator();
        var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        this.meterProviderForHistogramBatchWith4Dimensions = Sdk.CreateMeterProviderBuilder()
            .AddMeter("GenerateHistogramBatchWith4Dimensions")
            .AddReader(batchGeneratorReader)
            .Build();

        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        for (int i = 0; i < 1000; i++)
        {
            histogram.Record(randomForHistogram.Next(1, 1000), tags);
        }

        this.meterProviderForHistogramBatchWith4Dimensions.ForceFlush();
        return batchGeneratorExporter.Batch;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.meterWithNoListener?.Dispose();
        this.meterWithListener?.Dispose();
        this.meterWithDummyReader?.Dispose();
        this.meterWithGenevaMetricExporter?.Dispose();
        this.listener?.Dispose();
        this.meterProviderWithDummyReader?.Dispose();
        this.meterProviderWithGenevaMetricExporter?.Dispose();
        this.meterProviderForCounterBatchWith3Dimensions?.Dispose();
        this.meterProviderForCounterBatchWith4Dimensions?.Dispose();
        this.meterProviderForHistogramBatchWith3Dimensions?.Dispose();
        this.meterProviderForHistogramBatchWith4Dimensions?.Dispose();
        this.tlvMetricsExporter?.Dispose();
    }

    [Benchmark]
    public void InstrumentWithNoListener3Dimensions()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);
        this.counterWithNoListener?.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void InstrumentWithNoListener4Dimensions()
    {
        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        // 2 * 5 * 10 * 10 = 1000 time series max.
        this.counterWithNoListener?.Add(100, tags);
    }

    [Benchmark]
    public void InstrumentWithWithListener3Dimensions()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);
        this.counterWithListener?.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void InstrumentWithWithListener4Dimensions()
    {
        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        // 2 * 5 * 10 * 10 = 1000 time series max.
        this.counterWithListener?.Add(100, tags);
    }

    [Benchmark]
    public void InstrumentWithWithDummyReader3Dimensions()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);
        this.counterWithDummyReader?.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void InstrumentWithWithDummyReader4Dimensions()
    {
        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        // 2 * 5 * 10 * 10 = 1000 time series max.
        this.counterWithDummyReader?.Add(100, tags);
    }

    [Benchmark]
    public void InstrumentWithWithGenevaCounterMetricExporter3Dimensions()
    {
        var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[this.random.Value.Next(0, 10)]);
        var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[this.random.Value.Next(0, 10)]);
        this.counterWithGenevaMetricExporter?.Add(100, tag1, tag2, tag3);
    }

    [Benchmark]
    public void InstrumentWithWithGenevaCounterMetricExporter4Dimensions()
    {
        var tags = new TagList
        {
            { "DimName1", dimensionValues[this.random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[this.random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[this.random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[this.random.Value.Next(0, 10)] },
        };

        // 2 * 5 * 10 * 10 = 1000 time series max.
        this.counterWithGenevaMetricExporter?.Add(100, tags);
    }

    [Benchmark]
    public void SerializeCounterMetricItemWith3Dimensions()
    {
        this.tlvMetricsExporter.SerializeMetricWithTLV(
            MetricEventType.ULongMetric,
            this.counterMetricWith3Dimensions.Name,
            this.counterMetricPointWith3Dimensions.EndTime.ToFileTime(),
            this.counterMetricPointWith3Dimensions.Tags,
            this.counterMetricDataWith3Dimensions,
#if EXPOSE_EXPERIMENTAL_FEATURES
            Array.Empty<Exemplar>(),
#endif
            out _,
            out _);
    }

    [Benchmark]
    public void SerializeCounterMetricItemWith4Dimensions()
    {
        this.tlvMetricsExporter.SerializeMetricWithTLV(
            MetricEventType.ULongMetric,
            this.counterMetricWith4Dimensions.Name,
            this.counterMetricPointWith4Dimensions.EndTime.ToFileTime(),
            this.counterMetricPointWith4Dimensions.Tags,
            this.counterMetricDataWith4Dimensions,
#if EXPOSE_EXPERIMENTAL_FEATURES
            Array.Empty<Exemplar>(),
#endif
            out _,
            out _);
    }

    [Benchmark]
    public void ExportCounterMetricItemWith3Dimensions()
    {
        this.tlvMetricsExporter.Export(this.counterMetricBatchWith3Dimensions);
    }

    [Benchmark]
    public void ExportCounterMetricItemWith4Dimensions()
    {
        this.tlvMetricsExporter.Export(this.counterMetricBatchWith4Dimensions);
    }

    [Benchmark]
    public void SerializeHistogramMetricItemWith3Dimensions()
    {
        this.tlvMetricsExporter.SerializeHistogramMetricWithTLV(
            this.histogramMetricWith3Dimensions.Name,
            this.histogramMetricPointWith3Dimensions.EndTime.ToFileTime(),
            this.histogramMetricPointWith3Dimensions.Tags,
            this.histogramMetricPointWith3Dimensions.GetHistogramBuckets(),
            this.histogramSumWith3Dimensions,
            this.histogramCountWith3Dimensions,
            this.histogramMinWith3Dimensions,
            this.histogramMaxWith3Dimensions,
#if EXPOSE_EXPERIMENTAL_FEATURES
            Array.Empty<Exemplar>(),
#endif
            out _,
            out _);
    }

    [Benchmark]
    public void SerializeHistogramMetricItemWith4Dimensions()
    {
        this.tlvMetricsExporter.SerializeHistogramMetricWithTLV(
            this.histogramMetricWith4Dimensions.Name,
            this.histogramMetricPointWith4Dimensions.EndTime.ToFileTime(),
            this.histogramMetricPointWith4Dimensions.Tags,
            this.histogramMetricPointWith4Dimensions.GetHistogramBuckets(),
            this.histogramSumWith4Dimensions,
            this.histogramCountWith4Dimensions,
            this.histogramMinWith4Dimensions,
            this.histogramMaxWith4Dimensions,
#if EXPOSE_EXPERIMENTAL_FEATURES
            Array.Empty<Exemplar>(),
#endif
            out _,
            out _);
    }

    [Benchmark]
    public void ExportHistogramMetricItemWith3Dimensions()
    {
        this.tlvMetricsExporter.Export(this.histogramMetricBatchWith3Dimensions);
    }

    [Benchmark]
    public void ExportHistogramMetricItemWith4Dimensions()
    {
        this.tlvMetricsExporter.Export(this.histogramMetricBatchWith4Dimensions);
    }

    private class DummyReader : BaseExportingMetricReader
    {
        public DummyReader(BaseExporter<Metric> exporter)
            : base(exporter)
        {
        }
    }

    private class DummyMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    private class BatchGenerator : BaseExporter<Metric>
    {
        public Batch<Metric> Batch { get; set; }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            this.Batch = batch;
            return ExportResult.Success;
        }
    }
}

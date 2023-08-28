// <copyright file="MetricExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|                                                   Method |      Mean |    Error |   StdDev | Allocated |
|--------------------------------------------------------- |----------:|---------:|---------:|----------:|
|                      InstrumentWithNoListener3Dimensions |  66.17 ns | 0.316 ns | 0.296 ns |         - |
|                      InstrumentWithNoListener4Dimensions | 112.15 ns | 0.540 ns | 0.478 ns |         - |
|                    InstrumentWithWithListener3Dimensions |  70.17 ns | 0.110 ns | 0.097 ns |         - |
|                    InstrumentWithWithListener4Dimensions | 114.77 ns | 0.343 ns | 0.304 ns |         - |
|                 InstrumentWithWithDummyReader3Dimensions | 192.32 ns | 0.369 ns | 0.288 ns |         - |
|                 InstrumentWithWithDummyReader4Dimensions | 261.23 ns | 3.515 ns | 2.745 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter3Dimensions | 198.50 ns | 1.153 ns | 1.022 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter4Dimensions | 261.38 ns | 1.856 ns | 1.736 ns |         - |
|                SerializeCounterMetricItemWith3Dimensions | 215.62 ns | 0.759 ns | 0.673 ns |         - |
|                SerializeCounterMetricItemWith4Dimensions | 241.33 ns | 0.466 ns | 0.364 ns |         - |
|                   ExportCounterMetricItemWith3Dimensions | 257.88 ns | 0.572 ns | 0.507 ns |         - |
|                   ExportCounterMetricItemWith4Dimensions | 285.43 ns | 1.300 ns | 1.216 ns |         - |
|              SerializeHistogramMetricItemWith3Dimensions | 380.58 ns | 6.226 ns | 5.823 ns |         - |
|              SerializeHistogramMetricItemWith4Dimensions | 404.64 ns | 3.981 ns | 3.723 ns |         - |
|                 ExportHistogramMetricItemWith3Dimensions | 438.35 ns | 2.442 ns | 2.040 ns |         - |
|                 ExportHistogramMetricItemWith4Dimensions | 477.16 ns | 2.555 ns | 2.133 ns |         - |
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
    private GenevaMetricExporter exporter;
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
        this.exporter = new GenevaMetricExporter(exporterOptions);

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
        this.exporter?.Dispose();
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
        this.exporter.SerializeMetricWithTLV(
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
        this.exporter.SerializeMetricWithTLV(
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
        this.exporter.Export(this.counterMetricBatchWith3Dimensions);
    }

    [Benchmark]
    public void ExportCounterMetricItemWith4Dimensions()
    {
        this.exporter.Export(this.counterMetricBatchWith4Dimensions);
    }

    [Benchmark]
    public void SerializeHistogramMetricItemWith3Dimensions()
    {
        this.exporter.SerializeHistogramMetricWithTLV(
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
        this.exporter.SerializeHistogramMetricWithTLV(
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
        this.exporter.Export(this.histogramMetricBatchWith3Dimensions);
    }

    [Benchmark]
    public void ExportHistogramMetricItemWith4Dimensions()
    {
        this.exporter.Export(this.histogramMetricBatchWith4Dimensions);
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

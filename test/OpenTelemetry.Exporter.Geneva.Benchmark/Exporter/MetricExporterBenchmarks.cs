using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.102
  [Host]     : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT
  DefaultJob : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT

|                                                   Method |      Mean |    Error |   StdDev | Allocated |
|--------------------------------------------------------- |----------:|---------:|---------:|----------:|
|                      InstrumentWithNoListener3Dimensions |  66.07 ns | 0.213 ns | 0.199 ns |         - |
|                      InstrumentWithNoListener4Dimensions | 109.53 ns | 0.267 ns | 0.236 ns |         - |
|                    InstrumentWithWithListener3Dimensions |  65.59 ns | 0.322 ns | 0.285 ns |         - |
|                    InstrumentWithWithListener4Dimensions | 117.56 ns | 0.655 ns | 0.613 ns |         - |
|                 InstrumentWithWithDummyReader3Dimensions | 182.75 ns | 0.787 ns | 0.698 ns |         - |
|                 InstrumentWithWithDummyReader4Dimensions | 244.04 ns | 1.268 ns | 1.186 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter3Dimensions | 181.74 ns | 0.595 ns | 0.527 ns |         - |
| InstrumentWithWithGenevaCounterMetricExporter4Dimensions | 265.85 ns | 3.214 ns | 3.006 ns |         - |
|                SerializeCounterMetricItemWith3Dimensions | 166.33 ns | 0.470 ns | 0.439 ns |         - |
|                SerializeCounterMetricItemWith4Dimensions | 200.02 ns | 0.546 ns | 0.510 ns |         - |
|                   ExportCounterMetricItemWith3Dimensions | 464.79 ns | 3.996 ns | 3.738 ns |         - |
|                   ExportCounterMetricItemWith4Dimensions | 504.02 ns | 6.362 ns | 5.951 ns |         - |
|              SerializeHistogramMetricItemWith3Dimensions | 260.47 ns | 1.364 ns | 1.276 ns |         - |
|              SerializeHistogramMetricItemWith4Dimensions | 293.25 ns | 0.674 ns | 0.631 ns |         - |
|                 ExportHistogramMetricItemWith3Dimensions | 585.69 ns | 5.137 ns | 4.805 ns |         - |
|                 ExportHistogramMetricItemWith4Dimensions | 618.47 ns | 4.946 ns | 4.384 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
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
        private MetricData histogramSumWith3Dimensions;
        private MetricData histogramSumWith4Dimensions;
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

            counterWithNoListener = meterWithNoListener.CreateCounter<long>("counter");
            counterWithListener = meterWithListener.CreateCounter<long>("counter");
            counterWithDummyReader = meterWithDummyReader.CreateCounter<long>("counter");
            counterWithGenevaMetricExporter = meterWithGenevaMetricExporter.CreateCounter<long>("counter");

            var exporterOptions = new GenevaMetricExporterOptions() { ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace" };
            exporter = new GenevaMetricExporter(exporterOptions);

            counterMetricPointWith3Dimensions = GenerateCounterMetricItemWith3Dimensions(out counterMetricDataWith3Dimensions);
            counterMetricPointWith4Dimensions = GenerateCounterMetricItemWith4Dimensions(out counterMetricDataWith4Dimensions);

            counterMetricBatchWith3Dimensions = GenerateCounterBatchWith3Dimensions();
            counterMetricBatchWith4Dimensions = GenerateCounterBatchWith4Dimensions();

            using var enumeratorForCounterBatchWith3Dimensions = counterMetricBatchWith3Dimensions.GetEnumerator();
            enumeratorForCounterBatchWith3Dimensions.MoveNext();
            counterMetricWith3Dimensions = enumeratorForCounterBatchWith3Dimensions.Current;

            using var enumeratorForCounterBatchWith4Dimensions = counterMetricBatchWith4Dimensions.GetEnumerator();
            enumeratorForCounterBatchWith4Dimensions.MoveNext();
            counterMetricWith4Dimensions = enumeratorForCounterBatchWith4Dimensions.Current;


            histogramMetricPointWith3Dimensions = GenerateHistogramMetricItemWith3Dimensions(out histogramSumWith3Dimensions, out histogramCountWith3Dimensions);
            histogramMetricPointWith4Dimensions = GenerateHistogramMetricItemWith4Dimensions(out histogramSumWith4Dimensions, out histogramCountWith4Dimensions);

            histogramMetricBatchWith3Dimensions = GenerateHistogramBatchWith3Dimensions();
            histogramMetricBatchWith4Dimensions = GenerateHistogramBatchWith4Dimensions();

            using var enumeratorForHistogramBatchWith3Dimensions = histogramMetricBatchWith3Dimensions.GetEnumerator();
            enumeratorForHistogramBatchWith3Dimensions.MoveNext();
            histogramMetricWith3Dimensions = enumeratorForHistogramBatchWith3Dimensions.Current;

            using var enumeratorForHistogramBatchWith4Dimensions = histogramMetricBatchWith4Dimensions.GetEnumerator();
            enumeratorForHistogramBatchWith4Dimensions.MoveNext();
            histogramMetricWith4Dimensions = enumeratorForHistogramBatchWith4Dimensions.Current;

            #region Setup MeterListener
            listener = new MeterListener();
            listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterWithListener.Name)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            listener.Start();
            #endregion

            meterProviderWithDummyReader = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterWithDummyReader.Name)
            .AddReader(new DummyReader(new DummyMetricExporter()))
            .Build();

            meterProviderWithGenevaMetricExporter = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterWithGenevaMetricExporter.Name)
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
                Temporality = AggregationTemporality.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateCounterMetricItemWith3Dimensions")
                .AddReader(inMemoryReader)
                .Build();

            counter.Add(
                100,
                new("DimName1", dimensionValues[random.Value.Next(0, 10)]),
                new("DimName2", dimensionValues[random.Value.Next(0, 10)]),
                new("DimName3", dimensionValues[random.Value.Next(0, 10)]));

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
                Temporality = AggregationTemporality.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateCounterMetricItemWith4Dimensions")
                .AddReader(inMemoryReader)
                .Build();

            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
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
                Temporality = AggregationTemporality.Delta,
            };

            meterProviderForCounterBatchWith3Dimensions = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateCounterBatchWith3Dimensions")
                .AddReader(batchGeneratorReader)
                .Build();

            counter.Add(
                100,
                new("DimName1", dimensionValues[random.Value.Next(0, 10)]),
                new("DimName2", dimensionValues[random.Value.Next(0, 10)]),
                new("DimName3", dimensionValues[random.Value.Next(0, 10)]));

            meterProviderForCounterBatchWith3Dimensions.ForceFlush();
            return batchGeneratorExporter.Batch;
        }

        private Batch<Metric> GenerateCounterBatchWith4Dimensions()
        {
            using var meterWithInMemoryExporter = new Meter("GenerateCounterBatchWith4Dimensions", "0.0.1");
            var counter = meterWithInMemoryExporter.CreateCounter<long>("CounterWith4Dimensions");

            var batchGeneratorExporter = new BatchGenerator();
            var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
            {
                Temporality = AggregationTemporality.Delta,
            };

            meterProviderForCounterBatchWith4Dimensions = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateCounterBatchWith4Dimensions")
                .AddReader(batchGeneratorReader)
                .Build();

            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            counter.Add(100, tags);

            meterProviderForCounterBatchWith4Dimensions.ForceFlush();
            return batchGeneratorExporter.Batch;
        }

        private MetricPoint GenerateHistogramMetricItemWith3Dimensions(out MetricData sum, out uint count)
        {
            using var meterWithInMemoryExporter = new Meter("GenerateHistogramMetricItemWith3Dimensions", "0.0.1");
            var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith3Dimensions");

            var exportedItems = new List<Metric>();
            using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
            {
                Temporality = AggregationTemporality.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateHistogramMetricItemWith3Dimensions")
                .AddReader(inMemoryReader)
                .Build();

            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);

            for (int i = 0; i < 1000; i++)
            {
                histogram.Record(randomForHistogram.Next(1, 1000), tag1, tag2, tag3);
            }

            inMemoryReader.Collect();

            var metric = exportedItems[0];
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;
            sum = new MetricData { UInt64Value = Convert.ToUInt64(metricPoint.GetHistogramSum()) };
            count = Convert.ToUInt32(metricPoint.GetHistogramCount());

            return metricPoint;
        }

        private MetricPoint GenerateHistogramMetricItemWith4Dimensions(out MetricData sum, out uint count)
        {
            using var meterWithInMemoryExporter = new Meter("GenerateHistogramMetricItemWith4Dimensions", "0.0.1");
            var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith4Dimensions");

            var exportedItems = new List<Metric>();
            using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
            {
                Temporality = AggregationTemporality.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateHistogramMetricItemWith4Dimensions")
                .AddReader(inMemoryReader)
                .Build();

            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
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
            sum = new MetricData { UInt64Value = Convert.ToUInt64(metricPoint.GetHistogramSum()) };
            count = Convert.ToUInt32(metricPoint.GetHistogramCount());

            return metricPoint;
        }


        private Batch<Metric> GenerateHistogramBatchWith3Dimensions()
        {
            using var meterWithInMemoryExporter = new Meter("GenerateHistogramBatchWith3Dimensions", "0.0.1");
            var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith3Dimensions");

            var batchGeneratorExporter = new BatchGenerator();
            var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
            {
                Temporality = AggregationTemporality.Delta,
            };

            meterProviderForHistogramBatchWith3Dimensions = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateHistogramBatchWith3Dimensions")
                .AddReader(batchGeneratorReader)
                .Build();

            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);

            for (int i = 0; i < 1000; i++)
            {
                histogram.Record(randomForHistogram.Next(1, 1000), tag1, tag2, tag3);
            }

            meterProviderForHistogramBatchWith3Dimensions.ForceFlush();
            return batchGeneratorExporter.Batch;
        }

        private Batch<Metric> GenerateHistogramBatchWith4Dimensions()
        {
            using var meterWithInMemoryExporter = new Meter("GenerateHistogramBatchWith4Dimensions", "0.0.1");
            var histogram = meterWithInMemoryExporter.CreateHistogram<long>("HistogramWith4Dimensions");

            var batchGeneratorExporter = new BatchGenerator();
            var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter)
            {
                Temporality = AggregationTemporality.Delta,
            };

            meterProviderForHistogramBatchWith4Dimensions = Sdk.CreateMeterProviderBuilder()
                .AddMeter("GenerateHistogramBatchWith4Dimensions")
                .AddReader(batchGeneratorReader)
                .Build();

            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            for (int i = 0; i < 1000; i++)
            {
                histogram.Record(randomForHistogram.Next(1, 1000), tags);
            }

            meterProviderForHistogramBatchWith4Dimensions.ForceFlush();
            return batchGeneratorExporter.Batch;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            meterWithNoListener?.Dispose();
            meterWithListener?.Dispose();
            meterWithDummyReader?.Dispose();
            meterWithGenevaMetricExporter?.Dispose();
            listener?.Dispose();
            meterProviderWithDummyReader?.Dispose();
            meterProviderWithGenevaMetricExporter?.Dispose();
            meterProviderForCounterBatchWith3Dimensions?.Dispose();
            meterProviderForCounterBatchWith4Dimensions?.Dispose();
            meterProviderForHistogramBatchWith3Dimensions?.Dispose();
            meterProviderForHistogramBatchWith4Dimensions?.Dispose();
            exporter?.Dispose();
        }

        [Benchmark]
        public void InstrumentWithNoListener3Dimensions()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);
            counterWithNoListener?.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void InstrumentWithNoListener4Dimensions()
        {
            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            // 2 * 5 * 10 * 10 = 1000 time series max.
            counterWithNoListener?.Add(100, tags);
        }

        [Benchmark]
        public void InstrumentWithWithListener3Dimensions()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);
            counterWithListener?.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void InstrumentWithWithListener4Dimensions()
        {
            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            // 2 * 5 * 10 * 10 = 1000 time series max.
            counterWithListener?.Add(100, tags);
        }

        [Benchmark]
        public void InstrumentWithWithDummyReader3Dimensions()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);
            counterWithDummyReader?.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void InstrumentWithWithDummyReader4Dimensions()
        {
            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            // 2 * 5 * 10 * 10 = 1000 time series max.
            counterWithDummyReader?.Add(100, tags);
        }

        [Benchmark]
        public void InstrumentWithWithGenevaCounterMetricExporter3Dimensions()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Value.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Value.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Value.Next(0, 10)]);
            counterWithGenevaMetricExporter?.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void InstrumentWithWithGenevaCounterMetricExporter4Dimensions()
        {
            var tags = new TagList
        {
            { "DimName1", dimensionValues[random.Value.Next(0, 2)] },
            { "DimName2", dimensionValues[random.Value.Next(0, 5)] },
            { "DimName3", dimensionValues[random.Value.Next(0, 10)] },
            { "DimName4", dimensionValues[random.Value.Next(0, 10)] },
        };

            // 2 * 5 * 10 * 10 = 1000 time series max.
            counterWithGenevaMetricExporter?.Add(100, tags);
        }

        [Benchmark]
        public void SerializeCounterMetricItemWith3Dimensions()
        {
            exporter.SerializeMetric(
                   MetricEventType.ULongMetric,
                   counterMetricWith3Dimensions.Name,
                   counterMetricPointWith3Dimensions.EndTime.ToFileTime(),
                   counterMetricPointWith3Dimensions.Tags,
                   counterMetricDataWith3Dimensions);
        }

        [Benchmark]
        public void SerializeCounterMetricItemWith4Dimensions()
        {
            exporter.SerializeMetric(
                MetricEventType.ULongMetric,
                counterMetricWith4Dimensions.Name,
                counterMetricPointWith4Dimensions.EndTime.ToFileTime(),
                counterMetricPointWith4Dimensions.Tags,
                counterMetricDataWith4Dimensions);
        }

        [Benchmark]
        public void ExportCounterMetricItemWith3Dimensions()
        {
            exporter.Export(counterMetricBatchWith3Dimensions);
        }

        [Benchmark]
        public void ExportCounterMetricItemWith4Dimensions()
        {
            exporter.Export(counterMetricBatchWith4Dimensions);
        }

        [Benchmark]
        public void SerializeHistogramMetricItemWith3Dimensions()
        {
            exporter.SerializeHistogramMetric(
                histogramMetricWith3Dimensions.Name,
                histogramMetricPointWith3Dimensions.EndTime.ToFileTime(),
                histogramMetricPointWith3Dimensions.Tags,
                histogramMetricPointWith3Dimensions.GetHistogramBuckets(),
                histogramSumWith3Dimensions,
                histogramCountWith3Dimensions);
        }

        [Benchmark]
        public void SerializeHistogramMetricItemWith4Dimensions()
        {
            exporter.SerializeHistogramMetric(
                histogramMetricWith4Dimensions.Name,
                histogramMetricPointWith4Dimensions.EndTime.ToFileTime(),
                histogramMetricPointWith4Dimensions.Tags,
                histogramMetricPointWith4Dimensions.GetHistogramBuckets(),
                histogramSumWith4Dimensions,
                histogramCountWith4Dimensions);
        }

        [Benchmark]
        public void ExportHistogramMetricItemWith3Dimensions()
        {
            exporter.Export(histogramMetricBatchWith3Dimensions);
        }

        [Benchmark]
        public void ExportHistogramMetricItemWith4Dimensions()
        {
            exporter.Export(histogramMetricBatchWith4Dimensions);
        }

        private class DummyReader : BaseExportingMetricReader
        {
            public DummyReader(BaseExporter<Metric> exporter) : base(exporter)
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
                Batch = batch;
                return ExportResult.Success;
            }
        }
    }
}

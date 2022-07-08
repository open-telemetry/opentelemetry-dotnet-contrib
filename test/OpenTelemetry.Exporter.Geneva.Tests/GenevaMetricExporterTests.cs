// <copyright file="GenevaMetricExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Kaitai;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Exporter.Geneva.Tests.MetricsContract;

namespace OpenTelemetry.Exporter.Geneva.Tests
{
    public class GenevaMetricExporterTests
    {
        [Fact]
        public void NullExporterOptions()
        {
            GenevaMetricExporterOptions exporterOptions = null;
            Assert.Throws<ArgumentNullException>(() => new GenevaMetricExporter(exporterOptions));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void InvalidConnectionString(string connectionString)
        {
            var exporterOptions = new GenevaMetricExporterOptions() { ConnectionString = connectionString };
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                using var exporter = new GenevaMetricExporter(exporterOptions);
            });
        }

        [Fact]
        public void ParseConnectionStringCorrectly()
        {
            string path = string.Empty;
            Socket server = null;
            try
            {
                var exporterOptions = new GenevaMetricExporterOptions();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    exporterOptions.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                }
                else
                {
                    path = GenerateTempFilePath();
                    exporterOptions.ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                    var endpoint = new UnixDomainSocketEndPoint(path);
                    server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    server.Bind(endpoint);
                    server.Listen(1);
                }

                using var exporter = new GenevaMetricExporter(exporterOptions);
                var monitoringAccount = typeof(GenevaMetricExporter).GetField("monitoringAccount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
                var metricNamespace = typeof(GenevaMetricExporter).GetField("metricNamespace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
                Assert.Equal("OTelMonitoringAccount", monitoringAccount);
                Assert.Equal("OTelMetricNamespace", metricNamespace);
            }
            finally
            {
                server?.Dispose();
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SuccessfulSerialization(bool testMaxLimits)
        {
            using var meter = new Meter("SuccessfulSerialization", "0.0.1");
            var longCounter = meter.CreateCounter<long>("longCounter");
            var doubleCounter = meter.CreateCounter<double>("doubleCounter");
            var histogram = meter.CreateHistogram<long>("histogram");
            var exportedItems = new List<Metric>();
            using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("SuccessfulSerialization")
                .AddReader(inMemoryReader)
                .Build();

            long longValue = 123;
            double doubleValue = 123.45;

            if (testMaxLimits)
            {
                longValue = long.MaxValue;
                doubleValue = double.MaxValue;
            }

            longCounter.Add(
                longValue, new("tag1", "value1"), new("tag2", "value2"));

            doubleCounter.Add(
                doubleValue, new("tag1", "value1"), new("tag2", "value2"));

            meter.CreateObservableCounter(
                "observableLongCounter",
                () => new List<Measurement<long>>()
                {
                    new(longValue, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableCounter(
                "observableDoubleCounter",
                () => new List<Measurement<double>>()
                {
                    new(doubleValue, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableGauge(
                "observableLongGauge",
                () => new List<Measurement<long>>()
                {
                    new(longValue, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableGauge(
                "observableDoubleGauge",
                () => new List<Measurement<double>>()
                {
                    new(doubleValue, new("tag1", "value1"), new("tag2", "value2")),
                });

            if (testMaxLimits)
            {
                // only testing the max value allowed for sum
                // max value allowed for count is uint.MaxValue. It's not feasible to test that
                histogram.Record(longValue, new("tag1", "value1"), new("tag2", "value2"));
            }
            else
            {
                // Record the following values from Histogram:
                // (-inf - 0] : 1
                // (0 - 5] : 0
                // (5 - 10] : 0
                // (10 - 25] : 0
                // (25 - 50] : 0
                // (50 - 75] : 0
                // (75 - 100] : 0
                // (100 - 250] : 2
                // (250 - 500] : 0
                // (500 - 1000] : 1
                // (1000 - +inf) : 1
                //
                // The corresponding value-count pairs to be sent for the given distribution:
                // 0: 1
                // 250: 2
                // 1000: 1
                // 1001: 1 (We use one greater than the last bound provided (1000 + 1) as the value for the overflow bucket)

                histogram.Record(-1, new("tag1", "value1"), new("tag2", "value2"));
                histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"));
                histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"));
                histogram.Record(750, new("tag1", "value1"), new("tag2", "value2"));
                histogram.Record(2500, new("tag1", "value1"), new("tag2", "value2"));
            }

            string path = string.Empty;
            Socket server = null;
            try
            {
                var exporterOptions = new GenevaMetricExporterOptions();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    exporterOptions.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                }
                else
                {
                    path = GenerateTempFilePath();
                    exporterOptions.ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                    var endpoint = new UnixDomainSocketEndPoint(path);
                    server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    server.Bind(endpoint);
                    server.Listen(1);
                }

                exporterOptions.PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };

                using var exporter = new GenevaMetricExporter(exporterOptions);

                inMemoryReader.Collect();

                Assert.Equal(7, exportedItems.Count);

                // check serialization for longCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

                // check serialization for doubleCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

                // check serialization for histogram
                this.CheckSerializationForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

                // check serialization for observableLongCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

                // check serialization for observableDoubleCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

                // check serialization for observableLongGauge
                this.CheckSerializationForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);

                // check serialization for observableDoubleGauge
                this.CheckSerializationForSingleMetricPoint(exportedItems[6], exporter, exporterOptions);
            }
            finally
            {
                server?.Dispose();
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Fact]
        public void SuccessfulSerializationWithViews()
        {
            using var meter = new Meter("SuccessfulSerializationWithViews", "0.0.1");
            var longCounter = meter.CreateCounter<long>("longCounter");
            var doubleCounter = meter.CreateCounter<double>("doubleCounter");
            var histogramWithCustomBounds = meter.CreateHistogram<long>("histogramWithCustomBounds");
            var histogramWithNoBounds = meter.CreateHistogram<long>("histogramWithNoBounds");
            var exportedItems = new List<Metric>();
            using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Delta,
            };

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("SuccessfulSerializationWithViews")
                .AddView("longCounter", "renamedLongCounter")
                .AddView("doubleCounter", new MetricStreamConfiguration { TagKeys = new string[] { "tag1" } })
                .AddView(
                    "histogramWithCustomBounds",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Name = "renamedhistogramWithCustomBounds",
                        Description = "modifiedDescription",
                        Boundaries = new double[] { 500, 1000 },
                    })
                .AddView(instrument =>
                {
                    if (instrument.Name == "histogramWithNoBounds")
                    {
                        return new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { } };
                    }

                    return null;
                })
                .AddView("observableLongCounter", MetricStreamConfiguration.Drop)
                .AddView("observableDoubleCounter", new MetricStreamConfiguration { TagKeys = new string[] { } })
                .AddView(instrument =>
                {
                    if (instrument.Name == "observableLongGauge")
                    {
                        return new MetricStreamConfiguration
                        {
                            Name = "renamedobservableLongGauge",
                            Description = "modifiedDescription",
                            TagKeys = new string[] { "tag1" },
                        };
                    }

                    return null;
                })
                .AddView(instrument =>
                {
                    if (instrument.Name == "observableDoubleGauge")
                    {
                        return MetricStreamConfiguration.Drop;
                    }

                    return null;
                })
                .AddReader(inMemoryReader)
                .Build();

            longCounter.Add(
                123, new("tag1", "value1"), new("tag2", "value2"));

            doubleCounter.Add(
                123.45, new("tag1", "value1"), new("tag2", "value2"));

            meter.CreateObservableCounter(
                "observableLongCounter",
                () => new List<Measurement<long>>()
                {
                    new(123, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableCounter(
                "observableDoubleCounter",
                () => new List<Measurement<double>>()
                {
                    new(123.45, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableGauge(
                "observableLongGauge",
                () => new List<Measurement<long>>()
                {
                    new(123, new("tag1", "value1"), new("tag2", "value2")),
                });

            meter.CreateObservableGauge(
                "observableDoubleGauge",
                () => new List<Measurement<double>>()
                {
                    new(123.45, new("tag1", "value1"), new("tag2", "value2")),
                });

            // Record the following values for histogramWithCustomBounds:
            // (-inf - 500] : 3
            // (500 - 1000] : 1
            // (1000 - +inf) : 1
            //
            // The corresponding value-count pairs to be sent for histogramWithCustomBounds:
            // 500: 3
            // 1000: 1
            // 1001: 1 (We use one greater than the last bound provided (1000 + 1) as the value for the overflow bucket)

            histogramWithCustomBounds.Record(-1, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithCustomBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithCustomBounds.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

            // Record the following values for histogramWithNoBounds:
            // (-inf - 500] : 3
            // (500 - 1000] : 1
            // (1000 - +inf) : 1
            //
            // Only `sum` and `count` are sent for histogramWithNoBounds
            // No value-count pairs are sent for histogramWithNoBounds

            histogramWithNoBounds.Record(-1, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithNoBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
            histogramWithNoBounds.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

            string path = string.Empty;
            Socket server = null;
            try
            {
                var exporterOptions = new GenevaMetricExporterOptions();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    exporterOptions.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                }
                else
                {
                    path = GenerateTempFilePath();
                    exporterOptions.ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                    var endpoint = new UnixDomainSocketEndPoint(path);
                    server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    server.Bind(endpoint);
                    server.Listen(1);
                }

                exporterOptions.PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };

                using var exporter = new GenevaMetricExporter(exporterOptions);

                inMemoryReader.Collect();

                Assert.Equal(6, exportedItems.Count);

                // observableLongCounter and observableDoubleGauge are dropped
                Assert.Empty(exportedItems.Where(item => item.Name == "observableLongCounter" || item.Name == "observableDoubleGauge"));

                // check serialization for longCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

                // check serialization for doubleCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

                // check serialization for histogramWithCustomBounds
                this.CheckSerializationForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

                // check serialization for histogramWithNoBounds
                this.CheckSerializationForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

                // check serialization for observableDoubleCounter
                this.CheckSerializationForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

                // check serialization for observableLongGauge
                this.CheckSerializationForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);
            }
            finally
            {
                server?.Dispose();
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Fact]
        public void SuccessfulExportOnLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string path = GenerateTempFilePath();
                var exportedItems = new List<Metric>();

                using var meter = new Meter("SuccessfulExportOnLinux", "0.0.1");
                var counter = meter.CreateCounter<long>("counter");

                using var inMemoryMeter = new Meter("InMemoryExportOnLinux", "0.0.1");
                var inMemoryCounter = inMemoryMeter.CreateCounter<long>("counter");

                try
                {
                    var endpoint = new UnixDomainSocketEndPoint(path);
                    using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    server.Bind(endpoint);
                    server.Listen(1);

                    using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
                    {
                        TemporalityPreference = MetricReaderTemporalityPreference.Delta,
                    };

                    // Set up two different providers as only one Metric Processor is allowed.
                    // TODO: Simplify the setup when multiple Metric processors are allowed.
                    using var meterProvider = Sdk.CreateMeterProviderBuilder()
                        .AddMeter("SuccessfulExportOnLinux")
                        .AddGenevaMetricExporter(options =>
                        {
                            options.ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
                            options.MetricExportIntervalMilliseconds = 5000;
                        })
                        .Build();

                    using var inMemoryMeterProvider = Sdk.CreateMeterProviderBuilder()
                        .AddMeter("InMemoryExportOnLinux")
                        .AddReader(inMemoryReader)
                        .Build();

                    using var serverSocket = server.Accept();
                    serverSocket.ReceiveTimeout = 15000;

                    // Create a test exporter to get byte data for validation of the data received via Socket.
                    var exporterOptions = new GenevaMetricExporterOptions() { ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace" };
                    using var exporter = new GenevaMetricExporter(exporterOptions);

                    // Emit a metric and grab a copy of internal buffer for validation.
                    counter.Add(
                        123,
                        new KeyValuePair<string, object>("tag1", "value1"),
                        new KeyValuePair<string, object>("tag2", "value2"));

                    inMemoryCounter.Add(
                        123,
                        new KeyValuePair<string, object>("tag1", "value1"),
                        new KeyValuePair<string, object>("tag2", "value2"));

                    // exportedItems list should have a single entry after the MetricReader.Collect call
                    inMemoryReader.Collect();

                    Assert.Single(exportedItems);

                    var metric = exportedItems[0];
                    var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
                    metricPointsEnumerator.MoveNext();
                    var metricPoint = metricPointsEnumerator.Current;
                    var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
                    var metricData = new MetricData { UInt64Value = metricDataValue };
                    var bodyLength = exporter.SerializeMetric(
                        MetricEventType.ULongMetric,
                        metric.Name,
                        metricPoint.EndTime.ToFileTime(),
                        metricPoint.Tags,
                        metricData);

                    // Wait a little more than the ExportInterval for the exporter to export the data.
                    Task.Delay(5500).Wait();

                    // Read the data sent via socket.
                    var receivedData = new byte[1024];
                    int receivedDataSize = serverSocket.Receive(receivedData);

                    var fixedPayloadLength = (int)typeof(GenevaMetricExporter).GetField("fixedPayloadStartIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter);

                    // The whole payload is sent to the Unix Domain Socket
                    // BinaryHeader (fixed payload) + variable payload which starts with MetricPayload
                    Assert.Equal(bodyLength + fixedPayloadLength, receivedDataSize);

                    var stream = new KaitaiStream(receivedData);
                    var data = new MetricsContract(stream);

                    Assert.Equal(metric.Name, data.Body.MetricName.Value);
                    Assert.Equal("OTelMonitoringAccount", data.Body.MetricAccount.Value);
                    Assert.Equal("OTelMetricNamespace", data.Body.MetricNamespace.Value);

                    var valueSection = data.Body.ValueSection as SingleUint64Value;
                    Assert.Equal(metricDataValue, valueSection.Value);

                    Assert.Equal(2, data.Body.NumDimensions);

                    int i = 0;
                    foreach (var tag in metricPoint.Tags)
                    {
                        Assert.Equal(tag.Key, data.Body.DimensionsNames[i].Value);
                        Assert.Equal(tag.Value, data.Body.DimensionsValues[i].Value);
                        i++;
                    }

                    Assert.Equal((ushort)MetricEventType.ULongMetric, data.EventId);
                    Assert.Equal(bodyLength, data.LenBody);
                }
                finally
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string GenerateTempFilePath()
        {
            while (true)
            {
                string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                if (!File.Exists(path))
                {
                    return path;
                }
            }
        }

        private void CheckSerializationForSingleMetricPoint(Metric metric, GenevaMetricExporter exporter, GenevaMetricExporterOptions exporterOptions)
        {
            var metricType = metric.MetricType;
            var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
            metricPointsEnumerator.MoveNext();
            var metricPoint = metricPointsEnumerator.Current;
            MetricsContract data = null;

            // Check metric value, timestamp, eventId, and length of payload
            if (metricType == MetricType.LongSum || metricType == MetricType.LongGauge)
            {
                var metricDataValue = metricType == MetricType.LongSum ?
                    Convert.ToUInt64(metricPoint.GetSumLong()) :
                    Convert.ToUInt64(metricPoint.GetGaugeLastValueLong());
                var metricData = new MetricData { UInt64Value = metricDataValue };
                var bodyLength = exporter.SerializeMetric(
                    MetricEventType.ULongMetric,
                    metric.Name,
                    metricPoint.EndTime.ToFileTime(),
                    metricPoint.Tags,
                    metricData);
                var buffer = typeof(GenevaMetricExporter).GetField("bufferForNonHistogramMetrics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
                var stream = new KaitaiStream(buffer);
                data = new MetricsContract(stream);
                var valueSection = data.Body.ValueSection as SingleUint64Value;
                Assert.Equal(metricDataValue, valueSection.Value);
                Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
                Assert.Equal((ushort)MetricEventType.ULongMetric, data.EventId);
                Assert.Equal(bodyLength, data.LenBody);
            }
            else if (metricType == MetricType.DoubleSum || metricType == MetricType.DoubleGauge)
            {
                var metricDataValue = metricType == MetricType.DoubleSum ?
                    metricPoint.GetSumDouble() :
                    metricPoint.GetGaugeLastValueDouble();
                var metricData = new MetricData { DoubleValue = metricDataValue };
                var bodyLength = exporter.SerializeMetric(
                    MetricEventType.DoubleMetric,
                    metric.Name,
                    metricPoint.EndTime.ToFileTime(),
                    metricPoint.Tags,
                    metricData);
                var buffer = typeof(GenevaMetricExporter).GetField("bufferForNonHistogramMetrics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
                var stream = new KaitaiStream(buffer);
                data = new MetricsContract(stream);
                var valueSection = data.Body.ValueSection as SingleDoubleValue;
                Assert.Equal(metricDataValue, valueSection.Value);
                Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
                Assert.Equal((ushort)MetricEventType.DoubleMetric, data.EventId);
                Assert.Equal(bodyLength, data.LenBody);
            }
            else if (metricType == MetricType.Histogram)
            {
                var sum = new MetricData { UInt64Value = Convert.ToUInt64(metricPoint.GetHistogramSum()) };
                var count = Convert.ToUInt32(metricPoint.GetHistogramCount());
                var bodyLength = exporter.SerializeHistogramMetric(
                                        metric.Name,
                                        metricPoint.EndTime.ToFileTime(),
                                        metricPoint.Tags,
                                        metricPoint.GetHistogramBuckets(),
                                        sum,
                                        count);
                var buffer = typeof(GenevaMetricExporter).GetField("bufferForHistogramMetrics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
                var stream = new KaitaiStream(buffer);
                data = new MetricsContract(stream);
                var valueSection = data.Body.ValueSection as ExtAggregatedUint64Value;
                var valueCountPairs = data.Body.Histogram.Body as HistogramValueCountPairs;

                Assert.Equal(0, data.Body.Histogram.Version);
                Assert.Equal(2, (int)data.Body.Histogram.Type);

                int listIterator = 0;
                int bucketsWithPositiveCount = 0;
                double lastExplicitBound = default;
                foreach (var bucket in metricPoint.GetHistogramBuckets())
                {
                    if (bucket.BucketCount > 0)
                    {
                        if (bucket.ExplicitBound != double.PositiveInfinity)
                        {
                            Assert.Equal(bucket.ExplicitBound, valueCountPairs.Columns[listIterator].Value);
                            lastExplicitBound = bucket.ExplicitBound;
                        }
                        else
                        {
                            Assert.Equal((ulong)lastExplicitBound + 1, valueCountPairs.Columns[listIterator].Value);
                        }

                        Assert.Equal(bucket.BucketCount, valueCountPairs.Columns[listIterator].Count);

                        listIterator++;
                        bucketsWithPositiveCount++;
                    }
                }

                Assert.Equal(bucketsWithPositiveCount, valueCountPairs.DistributionSize);

                Assert.Equal(count, valueSection.Count);
                Assert.Equal(Convert.ToUInt64(metricPoint.GetHistogramSum()), valueSection.Sum);
                Assert.Equal(0UL, valueSection.Min);
                Assert.Equal(0UL, valueSection.Max);
                Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
                Assert.Equal((ushort)MetricEventType.ExternallyAggregatedULongDistributionMetric, data.EventId);
                Assert.Equal(bodyLength, data.LenBody);
            }

            // Check metric name, account, and namespace
            var connectionStringBuilder = new ConnectionStringBuilder(exporterOptions.ConnectionString);
            Assert.Equal(metric.Name, data.Body.MetricName.Value);
            Assert.Equal(connectionStringBuilder.Account, data.Body.MetricAccount.Value);
            Assert.Equal(connectionStringBuilder.Namespace, data.Body.MetricNamespace.Value);

            var dimensionsCount = 0;
            if (exporterOptions.PrepopulatedMetricDimensions != null)
            {
                foreach (var entry in exporterOptions.PrepopulatedMetricDimensions)
                {
                    Assert.Contains(data.Body.DimensionsNames, dim => dim.Value == entry.Key);
                    Assert.Contains(data.Body.DimensionsValues, dim => dim.Value == Convert.ToString(entry.Value, CultureInfo.InvariantCulture));
                }

                dimensionsCount += exporterOptions.PrepopulatedMetricDimensions.Count;
            }

            // Check metric dimensions
            int i = 0;
            foreach (var item in exporterOptions.PrepopulatedMetricDimensions)
            {
                Assert.Equal(item.Key, data.Body.DimensionsNames[i].Value);
                Assert.Equal(item.Value, data.Body.DimensionsValues[i].Value);
                i++;
            }

            foreach (var tag in metricPoint.Tags)
            {
                Assert.Equal(tag.Key, data.Body.DimensionsNames[i].Value);
                Assert.Equal(tag.Value, data.Body.DimensionsValues[i].Value);
                i++;
            }

            dimensionsCount += metricPoint.Tags.Count;

            Assert.Equal(dimensionsCount, data.Body.NumDimensions);
        }
    }
}

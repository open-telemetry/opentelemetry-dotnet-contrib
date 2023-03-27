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
using System.Diagnostics;
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
using Xunit;
using static OpenTelemetry.Exporter.Geneva.Tests.MetricsContract;

namespace OpenTelemetry.Exporter.Geneva.Tests;

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
        var longUpDownCounter = meter.CreateUpDownCounter<long>("longUpDownCounter");
        var doubleUpDownCounter = meter.CreateUpDownCounter<double>("doubleUpDownCounter");
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

        longUpDownCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"));

        doubleUpDownCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"));

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

        meter.CreateObservableUpDownCounter(
            "observableUpDownLongCounter",
            () => new List<Measurement<long>>()
            {
                new(longValue, new("tag1", "value1"), new("tag2", "value2")),
            });

        meter.CreateObservableUpDownCounter(
            "observableUpDownDoubleCounter",
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

            histogram.Record(0, new("tag1", "value1"), new("tag2", "value2"));
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

            Assert.Equal(11, exportedItems.Count);

            // check serialization for longCounter
            CheckSerializationForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

            // check serialization for doubleCounter
            CheckSerializationForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

            // check serialization for longUpDownCounter
            CheckSerializationForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

            // check serialization for doubleUpDownCounter
            CheckSerializationForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

            // check serialization for histogram
            CheckSerializationForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

            // check serialization for observableLongCounter
            CheckSerializationForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);

            // check serialization for observableDoubleCounter
            CheckSerializationForSingleMetricPoint(exportedItems[6], exporter, exporterOptions);

            // check serialization for observableLongGauge
            CheckSerializationForSingleMetricPoint(exportedItems[7], exporter, exporterOptions);

            // check serialization for observableDoubleGauge
            CheckSerializationForSingleMetricPoint(exportedItems[8], exporter, exporterOptions);

            // check serialization for observableUpDownLongCounter
            CheckSerializationForSingleMetricPoint(exportedItems[9], exporter, exporterOptions);

            // check serialization for observableUpDownDoubleCounter
            CheckSerializationForSingleMetricPoint(exportedItems[10], exporter, exporterOptions);
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
        var histogramWithNoMinMax = meter.CreateHistogram<long>("histogramWithNoMinMax");
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
                    Boundaries = new double[] { 500, 1000, 10000 },
                })
            .AddView(instrument =>
            {
                if (instrument.Name == "histogramWithNoBounds")
                {
                    return new ExplicitBucketHistogramConfiguration { Boundaries = Array.Empty<double>() };
                }

                return null;
            })
            .AddView(
                "histogramWithNoMinMax",
                new HistogramConfiguration
                {
                    RecordMinMax = false,
                })
            .AddView("observableLongCounter", MetricStreamConfiguration.Drop)
            .AddView("observableDoubleCounter", new MetricStreamConfiguration { TagKeys = Array.Empty<string>() })
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
        // (1000 - 10000] : 0
        // (10000 - +inf) : 1
        //
        // The corresponding value-count pairs to be sent for histogramWithCustomBounds:
        // 500: 3
        // 1000: 1
        // 1001: 1 (We use one greater than the last bound provided (1000 + 1) as the value for the overflow bucket)

        histogramWithCustomBounds.Record(0, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(50000, new("tag1", "value1"), new("tag2", "value2"));

        // Record the following values for histogramWithNoBounds:
        // (-inf - 500] : 3
        // (500 - 1000] : 1
        // (1000 - +inf) : 1
        //
        // Only `sum` and `count` are sent for histogramWithNoBounds
        // No value-count pairs are sent for histogramWithNoBounds

        histogramWithNoBounds.Record(0, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

        histogramWithNoMinMax.Record(-1, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

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

            // observableLongCounter and observableDoubleGauge are dropped
            Assert.Empty(exportedItems.Where(item => item.Name == "observableLongCounter" || item.Name == "observableDoubleGauge"));

            // check serialization for longCounter
            CheckSerializationForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

            // check serialization for doubleCounter
            CheckSerializationForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

            // check serialization for histogramWithCustomBounds
            CheckSerializationForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

            // check serialization for histogramWithNoBounds
            CheckSerializationForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

            // check serialization for observableDoubleCounter
            CheckSerializationForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

            // check serialization for observableLongGauge
            CheckSerializationForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);
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

                var exemplars = metricPoint.GetExemplars();
                var bodyLength = exporter.SerializeMetricWithTLV(
                    MetricEventType.ULongMetric,
                    metric.Name,
                    metricPoint.EndTime.ToFileTime(),
                    metricPoint.Tags,
                    metricData,
                    exemplars);

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
                var userData = data.Body as UserdataV2;
                var fields = userData.Fields;

                Assert.Contains(fields, field => field.Type == PayloadTypes.MetricName && (field.Value as WrappedString).Value == metric.Name);
                Assert.Contains(fields, field => field.Type == PayloadTypes.AccountName && (field.Value as WrappedString).Value == "OTelMonitoringAccount");
                Assert.Contains(fields, field => field.Type == PayloadTypes.NamespaceName && (field.Value as WrappedString).Value == "OTelMetricNamespace");

                var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleUint64Value).Value as SingleUint64ValueV2;
                Assert.Equal(metricDataValue, valueSection.Value);

                var dimensions = fields.FirstOrDefault(field => field.Type == PayloadTypes.Dimensions).Value as Dimensions;
                Assert.Equal(2, dimensions.NumDimensions);

                int i = 0;
                foreach (var tag in metricPoint.Tags)
                {
                    Assert.Equal(tag.Key, dimensions.DimensionsNames[i].Value);
                    Assert.Equal(tag.Value, dimensions.DimensionsValues[i].Value);
                    i++;
                }

                Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisableMetricNameValidationTest(bool disableMetricNameValidation)
    {
        var instrumentNameRegexProperty = GenevaMetricExporter.GetOpenTelemetryInstrumentNameRegexProperty();
        var initialInstrumentNameRegexValue = instrumentNameRegexProperty.GetValue(null);
        Socket server = null;
        try
        {
            var exportedMetrics = new List<Metric>();

            using var meter = new Meter(Guid.NewGuid().ToString());

            using (var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddGenevaMetricExporter(options =>
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        options.ConnectionString = $"Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace;DisableMetricNameValidation={disableMetricNameValidation}";
                    }
                    else
                    {
                        var path = GenerateTempFilePath();
                        options.ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace;DisableMetricNameValidation={disableMetricNameValidation}";

                        var endpoint = new UnixDomainSocketEndPoint(path);
                        server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                        server.Bind(endpoint);
                        server.Listen(1);
                    }
                })
                .AddInMemoryExporter(exportedMetrics)
                .Build())
            {
                var counter = meter.CreateCounter<int>("count/invalid");
                counter.Add(1);
            }

            if (disableMetricNameValidation)
            {
                Assert.Single(exportedMetrics);
            }
            else
            {
                Assert.Empty(exportedMetrics);
            }
        }
        finally
        {
            instrumentNameRegexProperty.SetValue(null, initialInstrumentNameRegexValue);
#pragma warning disable CA1508
            server?.Dispose();
#pragma warning restore CA1508
        }
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    public void SuccessfulSerializationWithTLV(bool testMaxLimits, bool hasExemplars, bool isWithinAnActivityContext, bool hasFilteredTagsForExemplars)
    {
        using var meter = new Meter("SuccessfulSerialization", "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var doubleCounter = meter.CreateCounter<double>("doubleCounter");
        var longUpDownCounter = meter.CreateUpDownCounter<long>("longUpDownCounter");
        var doubleUpDownCounter = meter.CreateUpDownCounter<double>("doubleUpDownCounter");
        var histogram = meter.CreateHistogram<long>("histogram");
        var exportedItems = new List<Metric>();
        using var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter("SuccessfulSerialization")
            .AddReader(inMemoryReader);

        if (hasExemplars)
        {
            meterProviderBuilder.SetExemplarFilter(new AlwaysOnExemplarFilter());
        }

        if (hasFilteredTagsForExemplars)
        {
            meterProviderBuilder.AddView("*", new MetricStreamConfiguration { TagKeys = new string[] { "tag1", "tag2" } });
        }

        using var meterProvider = meterProviderBuilder.Build();

        long longValue = 123;
        double doubleValue = 123.45;

        if (testMaxLimits)
        {
            longValue = long.MaxValue;
            doubleValue = double.MaxValue;
        }

        Activity activity = null;

        if (isWithinAnActivityContext)
        {
            activity = new Activity("Custom Activity");
            activity.Start();
        }

        longCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));

        doubleCounter.Add(
            doubleValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));

        longUpDownCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));

        doubleUpDownCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));

        meter.CreateObservableCounter(
            "observableLongCounter",
            () => new List<Measurement<long>>()
            {
                new(longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        meter.CreateObservableCounter(
            "observableDoubleCounter",
            () => new List<Measurement<double>>()
            {
                new(doubleValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        meter.CreateObservableGauge(
            "observableLongGauge",
            () => new List<Measurement<long>>()
            {
                new(longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        meter.CreateObservableGauge(
            "observableDoubleGauge",
            () => new List<Measurement<double>>()
            {
                new(doubleValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        meter.CreateObservableUpDownCounter(
            "observableUpDownLongCounter",
            () => new List<Measurement<long>>()
            {
                new(longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        meter.CreateObservableUpDownCounter(
            "observableUpDownDoubleCounter",
            () => new List<Measurement<double>>()
            {
                new(doubleValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1")),
            });

        if (testMaxLimits)
        {
            // only testing the max value allowed for sum
            // max value allowed for count is uint.MaxValue. It's not feasible to test that
            histogram.Record(longValue, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
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

            histogram.Record(0, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
            histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
            histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
            histogram.Record(750, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
            histogram.Record(2500, new("tag1", "value1"), new("tag2", "value2"), new("filteredTag1", "filteredValue1"));
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

            Assert.Equal(11, exportedItems.Count);

            // check serialization for longCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

            // check serialization for doubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

            // check serialization for longUpDownCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

            // check serialization for doubleUpDownCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

            // check serialization for histogram
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

            // check serialization for observableLongCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);

            // check serialization for observableDoubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[6], exporter, exporterOptions);

            // check serialization for observableLongGauge
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[7], exporter, exporterOptions);

            // check serialization for observableDoubleGauge
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[8], exporter, exporterOptions);

            // check serialization for observableUpDownLongCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[9], exporter, exporterOptions);

            // check serialization for observableUpDownDoubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[10], exporter, exporterOptions);

            activity?.Stop();
        }
        finally
        {
            activity?.Dispose();
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
    public void SuccessfulSerializationWithTLVWithViews()
    {
        using var meter = new Meter("SuccessfulSerializationWithViews", "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var doubleCounter = meter.CreateCounter<double>("doubleCounter");
        var histogramWithCustomBounds = meter.CreateHistogram<long>("histogramWithCustomBounds");
        var histogramWithNoBounds = meter.CreateHistogram<long>("histogramWithNoBounds");
        var histogramWithNoMinMax = meter.CreateHistogram<long>("histogramWithNoMinMax");
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
                    Boundaries = new double[] { 500, 1000, 10000 },
                })
            .AddView(instrument =>
            {
                if (instrument.Name == "histogramWithNoBounds")
                {
                    return new ExplicitBucketHistogramConfiguration { Boundaries = Array.Empty<double>() };
                }

                return null;
            })
            .AddView(
                "histogramWithNoMinMax",
                new HistogramConfiguration
                {
                    RecordMinMax = false,
                })
            .AddView("observableLongCounter", MetricStreamConfiguration.Drop)
            .AddView("observableDoubleCounter", new MetricStreamConfiguration { TagKeys = Array.Empty<string>() })
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
        // (1000 - 10000] : 0
        // (10000 - +inf) : 1
        //
        // The corresponding value-count pairs to be sent for histogramWithCustomBounds:
        // 500: 3
        // 1000: 1
        // 1001: 1 (We use one greater than the last bound provided (1000 + 1) as the value for the overflow bucket)

        histogramWithCustomBounds.Record(0, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithCustomBounds.Record(50000, new("tag1", "value1"), new("tag2", "value2"));

        // Record the following values for histogramWithNoBounds:
        // (-inf - 500] : 3
        // (500 - 1000] : 1
        // (1000 - +inf) : 1
        //
        // Only `sum` and `count` are sent for histogramWithNoBounds
        // No value-count pairs are sent for histogramWithNoBounds

        histogramWithNoBounds.Record(0, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoBounds.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

        histogramWithNoMinMax.Record(-1, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(150, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(750, new("tag1", "value1"), new("tag2", "value2"));
        histogramWithNoMinMax.Record(2500, new("tag1", "value1"), new("tag2", "value2"));

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

            // observableLongCounter and observableDoubleGauge are dropped
            Assert.Empty(exportedItems.Where(item => item.Name == "observableLongCounter" || item.Name == "observableDoubleGauge"));

            // check serialization for longCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[0], exporter, exporterOptions);

            // check serialization for doubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[1], exporter, exporterOptions);

            // check serialization for histogramWithCustomBounds
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[2], exporter, exporterOptions);

            // check serialization for histogramWithNoBounds
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[3], exporter, exporterOptions);

            // check serialization for observableDoubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[4], exporter, exporterOptions);

            // check serialization for observableLongGauge
            CheckSerializationWithTLVForSingleMetricPoint(exportedItems[5], exporter, exporterOptions);
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

    private static void AssertHistogramBucketSerialization(HistogramBucket bucket, HistogramValueCountPairs valueCountPairs, int listIterator, double lastExplicitBound)
    {
        if (bucket.ExplicitBound != double.PositiveInfinity)
        {
            Assert.Equal(bucket.ExplicitBound, valueCountPairs.Columns[listIterator].Value);
        }
        else
        {
            Assert.Equal((ulong)lastExplicitBound + 1, valueCountPairs.Columns[listIterator].Value);
        }

        Assert.Equal(bucket.BucketCount, valueCountPairs.Columns[listIterator].Count);
    }

    private static void CheckSerializationForSingleMetricPoint(Metric metric, GenevaMetricExporter exporter, GenevaMetricExporterOptions exporterOptions)
    {
        var metricType = metric.MetricType;
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        MetricsContract data = null;
        Userdata userData = null;

        // Check metric value, timestamp, eventId, and length of payload
        if (metricType == MetricType.LongSum)
        {
            var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
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
            userData = data.Body as Userdata;
            var valueSection = userData.ValueSection as SingleUint64Value;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.ULongMetric, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.LongGauge)
        {
            var metricDataValue = Convert.ToDouble(metricPoint.GetGaugeLastValueLong());
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
            userData = data.Body as Userdata;
            var valueSection = userData.ValueSection as SingleDoubleValue;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.DoubleMetric, data.EventId);
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
            userData = data.Body as Userdata;
            var valueSection = userData.ValueSection as SingleDoubleValue;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.DoubleMetric, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.LongSumNonMonotonic || metricType == MetricType.DoubleSumNonMonotonic)
        {
            var metricDataValue = metricType == MetricType.LongSumNonMonotonic ?
                Convert.ToDouble(metricPoint.GetSumLong()) :
                Convert.ToDouble(metricPoint.GetSumDouble());
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
            userData = data.Body as Userdata;
            var valueSection = userData.ValueSection as SingleDoubleValue;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.DoubleMetric, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.Histogram)
        {
            var sum = new MetricData { UInt64Value = Convert.ToUInt64(metricPoint.GetHistogramSum()) };
            var count = Convert.ToUInt32(metricPoint.GetHistogramCount());

            var min = new MetricData { UInt64Value = 0 };
            var max = new MetricData { UInt64Value = 0 };

            if (metricPoint.TryGetHistogramMinMaxValues(out var minValue, out var maxValue))
            {
                min = new MetricData { UInt64Value = Convert.ToUInt64(minValue) };
                max = new MetricData { UInt64Value = Convert.ToUInt64(maxValue) };
            }

            var bodyLength = exporter.SerializeHistogramMetric(
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricPoint.GetHistogramBuckets(),
                sum,
                count,
                min,
                max);

            var buffer = typeof(GenevaMetricExporter).GetField("bufferForHistogramMetrics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            data = new MetricsContract(stream);
            userData = data.Body as Userdata;
            var valueSection = userData.ValueSection as ExtAggregatedUint64Value;
            var histogram = userData.Histogram;
            var valueCountPairs = userData.Histogram.Body as HistogramValueCountPairs;

            Assert.Equal(0, histogram.Version);
            Assert.Equal(2, (int)histogram.Type);

            int listIterator = 0;
            int bucketsWithPositiveCount = 0;
            double lastExplicitBound = default;
            foreach (var bucket in metricPoint.GetHistogramBuckets())
            {
                if (bucket.BucketCount > 0)
                {
                    AssertHistogramBucketSerialization(bucket, valueCountPairs, listIterator, lastExplicitBound);
                    listIterator++;
                    bucketsWithPositiveCount++;
                }

                lastExplicitBound = bucket.ExplicitBound;
            }

            Assert.Equal(bucketsWithPositiveCount, valueCountPairs.DistributionSize);

            Assert.Equal(count, valueSection.Count);
            Assert.Equal(Convert.ToUInt64(metricPoint.GetHistogramSum()), valueSection.Sum);
            Assert.Equal(minValue, valueSection.Min);
            Assert.Equal(maxValue, valueSection.Max);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.ExternallyAggregatedULongDistributionMetric, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }

        // Check metric name, account, and namespace
        var connectionStringBuilder = new ConnectionStringBuilder(exporterOptions.ConnectionString);
        Assert.Equal(metric.Name, userData.MetricName.Value);
        Assert.Equal(connectionStringBuilder.Account, userData.MetricAccount.Value);
        Assert.Equal(connectionStringBuilder.Namespace, userData.MetricNamespace.Value);

        var dimensionsCount = 0;
        if (exporterOptions.PrepopulatedMetricDimensions != null)
        {
            foreach (var entry in exporterOptions.PrepopulatedMetricDimensions)
            {
                Assert.Contains(userData.DimensionsNames, dim => dim.Value == entry.Key);
                Assert.Contains(userData.DimensionsValues, dim => dim.Value == Convert.ToString(entry.Value, CultureInfo.InvariantCulture));
            }

            dimensionsCount += exporterOptions.PrepopulatedMetricDimensions.Count;
        }

        // Check metric dimensions
        int i = 0;
        foreach (var item in exporterOptions.PrepopulatedMetricDimensions)
        {
            Assert.Equal(item.Key, userData.DimensionsNames[i].Value);
            Assert.Equal(item.Value, userData.DimensionsValues[i].Value);
            i++;
        }

        foreach (var tag in metricPoint.Tags)
        {
            Assert.Equal(tag.Key, userData.DimensionsNames[i].Value);
            Assert.Equal(tag.Value, userData.DimensionsValues[i].Value);
            i++;
        }

        dimensionsCount += metricPoint.Tags.Count;

        Assert.Equal(dimensionsCount, userData.NumDimensions);
    }

    private static void CheckSerializationWithTLVForSingleMetricPoint(Metric metric, GenevaMetricExporter exporter, GenevaMetricExporterOptions exporterOptions)
    {
        var metricType = metric.MetricType;
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        var exemplars = metricPoint.GetExemplars();

        List<TlvField> fields = null;

        // Check metric value, timestamp, eventId, and length of payload
        if (metricType == MetricType.LongSum)
        {
            var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
            var metricData = new MetricData { UInt64Value = metricDataValue };
            var bodyLength = exporter.SerializeMetricWithTLV(
                MetricEventType.ULongMetric,
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricData,
                exemplars);

            var buffer = typeof(GenevaMetricExporter).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            var data = new MetricsContract(stream);
            var userData = data.Body as UserdataV2;
            fields = userData.Fields;

            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleUint64Value).Value as SingleUint64ValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.LongGauge)
        {
            var metricDataValue = Convert.ToDouble(metricPoint.GetGaugeLastValueLong());
            var metricData = new MetricData { DoubleValue = metricDataValue };
            var bodyLength = exporter.SerializeMetricWithTLV(
                MetricEventType.DoubleMetric,
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricData,
                exemplars);

            var buffer = typeof(GenevaMetricExporter).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            var data = new MetricsContract(stream);
            var userData = data.Body as UserdataV2;
            fields = userData.Fields;

            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.DoubleSum || metricType == MetricType.DoubleGauge)
        {
            var metricDataValue = metricType == MetricType.DoubleSum ?
                metricPoint.GetSumDouble() :
                metricPoint.GetGaugeLastValueDouble();
            var metricData = new MetricData { DoubleValue = metricDataValue };
            var bodyLength = exporter.SerializeMetricWithTLV(
                MetricEventType.DoubleMetric,
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricData,
                exemplars);

            var buffer = typeof(GenevaMetricExporter).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            var data = new MetricsContract(stream);
            var userData = data.Body as UserdataV2;
            fields = userData.Fields;

            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.LongSumNonMonotonic || metricType == MetricType.DoubleSumNonMonotonic)
        {
            var metricDataValue = metricType == MetricType.LongSumNonMonotonic ?
                Convert.ToDouble(metricPoint.GetSumLong()) :
                Convert.ToDouble(metricPoint.GetSumDouble());
            var metricData = new MetricData { DoubleValue = metricDataValue };
            var bodyLength = exporter.SerializeMetricWithTLV(
                MetricEventType.DoubleMetric,
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricData,
                exemplars);

            var buffer = typeof(GenevaMetricExporter).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            var data = new MetricsContract(stream);
            var userData = data.Body as UserdataV2;
            fields = userData.Fields;

            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }
        else if (metricType == MetricType.Histogram)
        {
            var sum = Convert.ToUInt64(metricPoint.GetHistogramSum());
            var count = Convert.ToUInt32(metricPoint.GetHistogramCount());
            if (!metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
            {
                min = 0;
                max = 0;
            }

            var bodyLength = exporter.SerializeHistogramMetricWithTLV(
                metric.Name,
                metricPoint.EndTime.ToFileTime(),
                metricPoint.Tags,
                metricPoint.GetHistogramBuckets(),
                sum,
                count,
                min,
                max,
                exemplars);

            var buffer = typeof(GenevaMetricExporter).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as byte[];
            var stream = new KaitaiStream(buffer);
            var data = new MetricsContract(stream);
            var userData = data.Body as UserdataV2;
            fields = userData.Fields;

            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.ExtAggregatedUint64Value).Value as ExtAggregatedUint64ValueV2;
            var valueCountPairs = fields.FirstOrDefault(field => field.Type == PayloadTypes.HistogramUint64ValueCountPairs).Value as HistogramValueCountPairs;

            int listIterator = 0;
            int bucketsWithPositiveCount = 0;
            double lastExplicitBound = default;
            foreach (var bucket in metricPoint.GetHistogramBuckets())
            {
                if (bucket.BucketCount > 0)
                {
                    AssertHistogramBucketSerialization(bucket, valueCountPairs, listIterator, lastExplicitBound);
                    listIterator++;
                    bucketsWithPositiveCount++;
                }

                lastExplicitBound = bucket.ExplicitBound;
            }

            Assert.Equal(bucketsWithPositiveCount, valueCountPairs.DistributionSize);

            Assert.Equal(count, valueSection.Count);
            Assert.Equal(Convert.ToUInt64(metricPoint.GetHistogramSum()), valueSection.Sum);
            Assert.Equal(min, valueSection.Min);
            Assert.Equal(max, valueSection.Max);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
            Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
            Assert.Equal(bodyLength, data.LenBody);
        }

        if (exemplars.Length > 0)
        {
            var validExemplars = exemplars.Where(exemplar => exemplar.Timestamp != default).ToList();

            var exemplarsPayload = fields.FirstOrDefault(field => field.Type == PayloadTypes.Exemplars).Value as Exemplars;
            var singleExemplarList = exemplarsPayload.ExemplarList;

            Assert.Equal(0, exemplarsPayload.Version);
            Assert.True(singleExemplarList.All(singleExemplar => singleExemplar.Version == 0));

            Assert.Equal(validExemplars.Count, exemplarsPayload.NumberOfExemplars.Value);
            Assert.Equal(validExemplars.Count, singleExemplarList.Count);

            for (int i = 0; i < validExemplars.Count; i++)
            {
                var expectedExemplar = validExemplars[i];
                var serializedExemplar = singleExemplarList[i];

                AssertExemplarFilteredTagSerialization(expectedExemplar, serializedExemplar);
            }
        }

        // Check metric name, account, and namespace
        var connectionStringBuilder = new ConnectionStringBuilder(exporterOptions.ConnectionString);
        Assert.Contains(fields, field => field.Type == PayloadTypes.MetricName && (field.Value as WrappedString).Value == metric.Name);
        Assert.Contains(fields, field => field.Type == PayloadTypes.AccountName && (field.Value as WrappedString).Value == connectionStringBuilder.Account);
        Assert.Contains(fields, field => field.Type == PayloadTypes.NamespaceName && (field.Value as WrappedString).Value == connectionStringBuilder.Namespace);

        // Check dimensions
        var dimensions = fields.FirstOrDefault(field => field.Type == PayloadTypes.Dimensions).Value as Dimensions;
        var dimensionsCount = 0;
        if (exporterOptions.PrepopulatedMetricDimensions != null)
        {
            foreach (var entry in exporterOptions.PrepopulatedMetricDimensions)
            {
                Assert.Contains(dimensions.DimensionsNames, dim => dim.Value == entry.Key);
                Assert.Contains(dimensions.DimensionsValues, dim => dim.Value == Convert.ToString(entry.Value, CultureInfo.InvariantCulture));
            }

            dimensionsCount += exporterOptions.PrepopulatedMetricDimensions.Count;
        }

        // Check metric dimensions
        int index = 0;
        foreach (var item in exporterOptions.PrepopulatedMetricDimensions)
        {
            Assert.Equal(item.Key, dimensions.DimensionsNames[index].Value);
            Assert.Equal(item.Value, dimensions.DimensionsValues[index].Value);
            index++;
        }

        foreach (var tag in metricPoint.Tags)
        {
            Assert.Equal(tag.Key, dimensions.DimensionsNames[index].Value);
            Assert.Equal(tag.Value, dimensions.DimensionsValues[index].Value);
            index++;
        }

        dimensionsCount += metricPoint.Tags.Count;

        Assert.Equal(dimensionsCount, dimensions.NumDimensions);
    }

    private static void AssertExemplarFilteredTagSerialization(Exemplar expectedExemplar, SingleExemplar serializedExemplar)
    {
        var serializedExemplarBody = serializedExemplar.Body;

        var expectedUnixNanoSeconds = DateTime.FromFileTimeUtc(expectedExemplar.Timestamp.ToFileTime())
                .ToUniversalTime()
                .Subtract(new DateTime(1970, 1, 1))
                .TotalMilliseconds * 1000000;

        // TODO: Test for exemplar values stored as long
        if (!serializedExemplarBody.Value.IsDoubleStoredAsLong)
        {
            Assert.Equal(expectedExemplar.DoubleValue, serializedExemplarBody.Value.ValueAsDouble);
        }

        Assert.Equal((ulong)expectedUnixNanoSeconds, serializedExemplarBody.TimeUnixNano);

        if (expectedExemplar.TraceId.HasValue)
        {
            var traceIdBytes = new byte[16];
            expectedExemplar.TraceId.Value.CopyTo(traceIdBytes);

            Assert.Equal(16, serializedExemplarBody.TraceId.Length);
            Assert.True(traceIdBytes.SequenceEqual(serializedExemplarBody.TraceId));
        }

        if (expectedExemplar.SpanId.HasValue)
        {
            var spanIdBytes = new byte[8];
            expectedExemplar.SpanId.Value.CopyTo(spanIdBytes);

            Assert.Equal(8, serializedExemplarBody.SpanId.Length);
            Assert.True(spanIdBytes.SequenceEqual(serializedExemplarBody.SpanId));
        }

        if (expectedExemplar.FilteredTags != null && expectedExemplar.FilteredTags.Count > 0)
        {
            Assert.Equal(expectedExemplar.FilteredTags.Count, serializedExemplarBody.NumberOfLabels);

            for (int i = 0; i < expectedExemplar.FilteredTags.Count; i++)
            {
                var expectedFilteredTag = expectedExemplar.FilteredTags[i];
                var serializedFilteredTag = serializedExemplarBody.Labels[i];

                Assert.Equal(expectedFilteredTag.Key, serializedFilteredTag.Name.Value);
                Assert.Equal(expectedFilteredTag.Value, serializedFilteredTag.Value.Value);
            }
        }
    }
}

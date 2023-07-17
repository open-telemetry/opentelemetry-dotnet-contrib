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
using Kaitai;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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
    public void ParseUnixConnectionStringCorrectly()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string path = string.Empty;
        Socket server = null;
        try
        {
            path = GenerateTempFilePath();
            var endpoint = new UnixDomainSocketEndPoint(path);
            server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            server.Bind(endpoint);
            server.Listen(1);

            var exporterOptions = new GenevaMetricExporterOptions
            {
                ConnectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace",
            };
            using var exporter = new GenevaMetricExporter(exporterOptions);
            var monitoringAccount = typeof(GenevaMetricExporter).GetField("defaultMonitoringAccount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
            var metricNamespace = typeof(GenevaMetricExporter).GetField("defaultMetricNamespace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
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

    [Fact]
    public void ParseWindowsConnectionStringCorrectly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var exporterOptions = new GenevaMetricExporterOptions
        {
            ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace",
        };
        using var exporter = new GenevaMetricExporter(exporterOptions);
        var monitoringAccount = typeof(GenevaMetricExporter).GetField("defaultMonitoringAccount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
        var metricNamespace = typeof(GenevaMetricExporter).GetField("defaultMetricNamespace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(exporter) as string;
        Assert.Equal("OTelMonitoringAccount", monitoringAccount);
        Assert.Equal("OTelMetricNamespace", metricNamespace);
    }

    [Fact]
    public void CannotUseReservedDimensionsInPrepopulatedFields()
    {
        var exporterOptions = new GenevaMetricExporterOptions();
        var prepopulatedMetricDimensions = new Dictionary<string, object>
        {
            ["_microsoft_metrics_account"] = "MetricsAccount",
        };

        Assert.Throws<ArgumentException>(() => { exporterOptions.PrepopulatedMetricDimensions = prepopulatedMetricDimensions; });

        prepopulatedMetricDimensions = new Dictionary<string, object>
        {
            ["_microsoft_metrics_namespace"] = "MetricsNamespace",
        };

        Assert.Throws<ArgumentException>(() => { exporterOptions.PrepopulatedMetricDimensions = prepopulatedMetricDimensions; });

        prepopulatedMetricDimensions = new Dictionary<string, object>
        {
            ["_microsoft_metrics_account"] = "MetricsAccount",
            ["_microsoft_metrics_namespace"] = "MetricsNamespace",
        };

        Assert.Throws<ArgumentException>(() => { exporterOptions.PrepopulatedMetricDimensions = prepopulatedMetricDimensions; });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisableMetricNameValidationTest(bool disableMetricNameValidation)
    {
        var instrumentNameRegexProperty = GenevaMetricExporter.GetOpenTelemetryInstrumentNameRegexProperty();
        var initialInstrumentNameRegexValue = instrumentNameRegexProperty.GetValue(null);
        try
        {
            var exporterOptions = new GenevaMetricExporterOptions()
            {
                ConnectionString = $"Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace;DisableMetricNameValidation={disableMetricNameValidation}",
            };

            var exportedEvents = new List<(MetricEventType EventType, byte[] Body)>();
            var dataTransport = new Mock<IMetricDataTransport>();
            dataTransport
                .Setup(x => x.Send(It.IsAny<MetricEventType>(), It.IsAny<byte[]>(), It.IsAny<int>()))
                .Callback((MetricEventType eventType, byte[] body, int size) => exportedEvents.Add((eventType, TransportCopy(body, size))));

            using var exporter = new GenevaMetricExporter(exporterOptions, dataTransport.Object);
            using var inMemoryReader = new BaseExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Delta,
            };

            using var meter = new Meter(Guid.NewGuid().ToString());
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddReader(inMemoryReader)
                .Build();
            var counter = meter.CreateCounter<int>("count/invalid");
            counter.Add(1);

            inMemoryReader.Collect();

            if (disableMetricNameValidation)
            {
                Assert.Single(exportedEvents);
            }
            else
            {
                Assert.Empty(exportedEvents);
            }
        }
        finally
        {
            instrumentNameRegexProperty.SetValue(null, initialInstrumentNameRegexValue);
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

        const string defaultMonitoringAccount = "OTelMonitoringAccount";
        const string defaultNamespace = "OTelMetricNamespace";
        var exporterOptions = new GenevaMetricExporterOptions()
        {
            ConnectionString = $"Account={defaultMonitoringAccount};Namespace={defaultNamespace}",
            PrepopulatedMetricDimensions = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        };
        var exportedMetrics = new List<Metric>();
        var exportedEvents = new List<(MetricEventType EventType, byte[] Body)>();
        var dataTransport = new Mock<IMetricDataTransport>();
        dataTransport
            .Setup(x => x.Send(It.IsAny<MetricEventType>(), It.IsAny<byte[]>(), It.IsAny<int>()))
            .Callback((MetricEventType eventType, byte[] body, int size) => exportedEvents.Add((eventType, TransportCopy(body, size))));
        using var exporter = new GenevaMetricExporter(exporterOptions, dataTransport.Object);
        using var exportingReader = new BaseExportingMetricReader(new SpyingExporter(exportedMetrics, exporter))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter("SuccessfulSerialization")
            .AddReader(exportingReader);

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

        meterProvider.ForceFlush();
        Assert.Equal(11, exportedMetrics.Count);

        try
        {
            // check serialization for longCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[0], exportedEvents[0], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for doubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[1], exportedEvents[1], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for longUpDownCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[2], exportedEvents[2], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for doubleUpDownCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[3], exportedEvents[3], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for histogram
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[4], exportedEvents[4], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableLongCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[5], exportedEvents[5], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableDoubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[6], exportedEvents[6], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableLongGauge
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[7], exportedEvents[7], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableDoubleGauge
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[8], exportedEvents[8], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableUpDownLongCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[9], exportedEvents[9], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            // check serialization for observableUpDownDoubleCounter
            CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[10], exportedEvents[10], defaultMonitoringAccount, defaultNamespace, exporterOptions);

            activity?.Stop();
        }
        finally
        {
            activity?.Dispose();
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

        const string defaultMonitoringAccount = "OTelMonitoringAccount";
        const string defaultNamespace = "OTelMetricNamespace";
        var exporterOptions = new GenevaMetricExporterOptions()
        {
            ConnectionString = $"Account={defaultMonitoringAccount};Namespace={defaultNamespace}",
            PrepopulatedMetricDimensions = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        };

        var exportedMetrics = new List<Metric>();
        var exportedEvents = new List<(MetricEventType EventType, byte[] Body)>();
        var dataTransport = new Mock<IMetricDataTransport>();
        dataTransport
            .Setup(x => x.Send(It.IsAny<MetricEventType>(), It.IsAny<byte[]>(), It.IsAny<int>()))
            .Callback((MetricEventType eventType, byte[] body, int size) => exportedEvents.Add((eventType, TransportCopy(body, size))));
        using var exporter = new GenevaMetricExporter(exporterOptions, dataTransport.Object);
        using var exportingReader = new BaseExportingMetricReader(new SpyingExporter(exportedMetrics, exporter))
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
            .AddReader(exportingReader)
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

        meterProvider.ForceFlush();

        Assert.Equal(7, exportedEvents.Count);

        // check serialization for longCounter
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[0], exportedEvents[0], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for doubleCounter
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[1], exportedEvents[1], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for histogramWithCustomBounds
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[2], exportedEvents[2], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for histogramWithNoBounds
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[3], exportedEvents[3], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for histogramWithNoMinMax
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[4], exportedEvents[4], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for observableDoubleCounter
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[5], exportedEvents[5], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for observableLongGauge
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[6], exportedEvents[6], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // observableLongCounter and observableDoubleGauge are dropped
        Assert.Empty(exportedMetrics.Where(item => item.Name == "observableLongCounter" || item.Name == "observableDoubleGauge"));
    }

    [Fact]
    public void SuccessfulSerializationWithCustomAccountAndNamespace()
    {
        using var meter = new Meter("SuccessfulSerializationWithCustomAccountAndNamespace", "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        var doubleCounter = meter.CreateCounter<double>("doubleCounter");
        var histogram = meter.CreateHistogram<long>("histogram");

        const string defaultMonitoringAccount = "OTelMonitoringAccount";
        const string defaultNamespace = "OTelMetricNamespace";
        var exporterOptions = new GenevaMetricExporterOptions()
        {
            ConnectionString = $"Account={defaultMonitoringAccount};Namespace={defaultNamespace}",
            PrepopulatedMetricDimensions = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        };

        var exportedMetrics = new List<Metric>();
        var exportedEvents = new List<(MetricEventType EventType, byte[] Body)>();
        var dataTransport = new Mock<IMetricDataTransport>();
        dataTransport
            .Setup(x => x.Send(It.IsAny<MetricEventType>(), It.IsAny<byte[]>(), It.IsAny<int>()))
            .Callback((MetricEventType eventType, byte[] body, int size) => exportedEvents.Add((eventType, TransportCopy(body, size))));
        using var exporter = new GenevaMetricExporter(exporterOptions, dataTransport.Object);
        using var exportingReader = new BaseExportingMetricReader(new SpyingExporter(exportedMetrics, exporter))
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(exportingReader)
            .Build();

        long longValue = 123;
        double doubleValue = 123.45;

        longCounter.Add(
            longValue, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForLongCounter"));

        doubleCounter.Add(
            doubleValue, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_namespace", "NamespaceForDoubleCounter"));

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

        histogram.Record(0, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForHistogram"), new("_microsoft_metrics_namespace", "NamespaceForHistogram"));
        histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForHistogram"), new("_microsoft_metrics_namespace", "NamespaceForHistogram"));
        histogram.Record(150, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForHistogram"), new("_microsoft_metrics_namespace", "NamespaceForHistogram"));
        histogram.Record(750, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForHistogram"), new("_microsoft_metrics_namespace", "NamespaceForHistogram"));
        histogram.Record(2500, new("tag1", "value1"), new("tag2", "value2"), new("_microsoft_metrics_account", "AccountForHistogram"), new("_microsoft_metrics_namespace", "NamespaceForHistogram"));

        meterProvider.ForceFlush();

        Assert.Equal(3, exportedEvents.Count);

        // check serialization for longCounter
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[0], exportedEvents[0], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for doubleCounter
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[1], exportedEvents[1], defaultMonitoringAccount, defaultNamespace, exporterOptions);

        // check serialization for histogram
        CheckSerializationWithTLVForSingleMetricPoint(exportedMetrics[2], exportedEvents[2], defaultMonitoringAccount, defaultNamespace, exporterOptions);
    }

    [Fact]
    public void AddGenevaMetricExporterNamedOptionsSupport()
    {
        string connectionString;
        string connectionStringForNamedOptions;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            connectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
            connectionStringForNamedOptions = "Account=OTelMonitoringAccount-NamedOptions;Namespace=OTelMetricNamespace-NamedOptions";
        }
        else
        {
            var path = GenerateTempFilePath();
            connectionString = $"Endpoint=unix:{path};Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";
            connectionStringForNamedOptions = $"Endpoint=unix:{path};Account=OTelMonitoringAccount-NamedOptions;Namespace=OTelMetricNamespace-NamedOptions";
        }

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<GenevaMetricExporterOptions>(options =>
                {
                    options.ConnectionString = connectionString;
                });
                services.Configure<GenevaMetricExporterOptions>("ExporterWithNamedOptions", options =>
                {
                    options.ConnectionString = connectionStringForNamedOptions;
                });
            })
            .AddGenevaMetricExporter(options =>
            {
                // ConnectionString for the options is already set in `IServiceCollection Configure<TOptions>` calls above
                Assert.Equal(connectionString, options.ConnectionString);
            })
            .AddGenevaMetricExporter("ExporterWithNamedOptions", options =>
            {
                // ConnectionString for the named options is already set in `IServiceCollection Configure<TOptions>` calls above
                Assert.Equal(connectionStringForNamedOptions, options.ConnectionString);
            })
            .Build();
    }

    [Fact]
    public void SuccessfulSerializationWithMeterNamespaceOverride()
    {
        var exporterOptions = new GenevaMetricExporterOptions
        {
            ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace",
            MeterNamespaceOverrides = new Dictionary<string, string>
            {
                ["SuccessfulSerializationWithMeterNamespaceOverride"] = "OverriddenNamespace",
            },
        };

        var exportedEvents = new List<(MetricEventType EventType, byte[] Body)>();
        var dataTransport = new Mock<IMetricDataTransport>();
        dataTransport
            .Setup(x => x.Send(It.IsAny<MetricEventType>(), It.IsAny<byte[]>(), It.IsAny<int>()))
            .Callback((MetricEventType eventType, byte[] body, int size) => exportedEvents.Add((eventType, TransportCopy(body, size))));

        using var exporter = new GenevaMetricExporter(exporterOptions, dataTransport.Object);

        using var inMemoryReader = new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("SuccessfulSerializationWithMeterNamespaceOverride")
            .AddReader(inMemoryReader)
            .Build();

        using var meter = new Meter("SuccessfulSerializationWithMeterNamespaceOverride", "0.0.1");
        var longCounter = meter.CreateCounter<long>("longCounter");
        longCounter.Add(123L, new("tag1", "value1"), new("tag2", "value2"));
        inMemoryReader.Collect();

        Assert.Single(exportedEvents);
        var stream = new KaitaiStream(exportedEvents[0].Body);
        var data = new MetricsContract(stream);
        var result = data.Body as UserdataV2;
        var fields = result.Fields;
        Assert.Contains(fields, field => field.Type == PayloadTypes.AccountName && (field.Value as WrappedString).Value == "OTelMonitoringAccount");
        Assert.Contains(fields, field => field.Type == PayloadTypes.NamespaceName && (field.Value as WrappedString).Value == "OverriddenNamespace");
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

    private static void CheckSerializationWithTLVForSingleMetricPoint(
        Metric metric,
        (MetricEventType EventType, byte[] Body) singleEvent,
        string defaultMonitoringAccount,
        string defaultMetricNamespace,
        GenevaMetricExporterOptions exporterOptions)
    {
        var metricType = metric.MetricType;
        var metricPointsEnumerator = metric.GetMetricPoints().GetEnumerator();
        metricPointsEnumerator.MoveNext();
        var metricPoint = metricPointsEnumerator.Current;
        var exemplars = metricPoint.GetExemplars();

        var userData = GetSerializedData(singleEvent);
        var fields = userData.Fields;

        // Check metric value, timestamp
        if (metricType == MetricType.LongSum)
        {
            var metricDataValue = Convert.ToUInt64(metricPoint.GetSumLong());
            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleUint64Value).Value as SingleUint64ValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
        }
        else if (metricType == MetricType.LongGauge)
        {
            var metricDataValue = Convert.ToDouble(metricPoint.GetGaugeLastValueLong());
            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
        }
        else if (metricType == MetricType.DoubleSum || metricType == MetricType.DoubleGauge)
        {
            var metricDataValue = metricType == MetricType.DoubleSum ?
                metricPoint.GetSumDouble() :
                metricPoint.GetGaugeLastValueDouble();
            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
        }
        else if (metricType == MetricType.LongSumNonMonotonic || metricType == MetricType.DoubleSumNonMonotonic)
        {
            var metricDataValue = metricType == MetricType.LongSumNonMonotonic ?
                Convert.ToDouble(metricPoint.GetSumLong()) :
                Convert.ToDouble(metricPoint.GetSumDouble());
            var valueSection = fields.FirstOrDefault(field => field.Type == PayloadTypes.SingleDoubleValue).Value as SingleDoubleValueV2;
            Assert.Equal(metricDataValue, valueSection.Value);
            Assert.Equal((ulong)metricPoint.EndTime.ToFileTime(), valueSection.Timestamp);
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

        var monitoringAccount = defaultMonitoringAccount;
        var metricNamespace = defaultMetricNamespace;
        foreach (var tag in metricPoint.Tags)
        {
            if (tag.Key.Equals("_microsoft_metrics_account", StringComparison.OrdinalIgnoreCase) && tag.Value is string accountValue)
            {
                if (!string.IsNullOrWhiteSpace(accountValue))
                {
                    monitoringAccount = accountValue;
                }
            }
            else if (tag.Key.Equals("_microsoft_metrics_namespace", StringComparison.OrdinalIgnoreCase) && tag.Value is string namespaceValue)
            {
                if (!string.IsNullOrWhiteSpace(namespaceValue))
                {
                    metricNamespace = namespaceValue;
                }
            }
        }

        Assert.Contains(fields, field => field.Type == PayloadTypes.MetricName && (field.Value as WrappedString).Value == metric.Name);
        Assert.Contains(fields, field => field.Type == PayloadTypes.AccountName && (field.Value as WrappedString).Value == monitoringAccount);
        Assert.Contains(fields, field => field.Type == PayloadTypes.NamespaceName && (field.Value as WrappedString).Value == metricNamespace);

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
        if (exporterOptions.PrepopulatedMetricDimensions != null)
        {
            foreach (var item in exporterOptions.PrepopulatedMetricDimensions)
            {
                Assert.Equal(item.Key, dimensions.DimensionsNames[index].Value);
                Assert.Equal(item.Value, dimensions.DimensionsValues[index].Value);
                index++;
            }
        }

        int reservedTags = 0;
        foreach (var tag in metricPoint.Tags)
        {
            if (tag.Key.Equals("_microsoft_metrics_account", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Equals("_microsoft_metrics_namespace", StringComparison.OrdinalIgnoreCase))
            {
                reservedTags++;
                continue;
            }

            Assert.Equal(tag.Key, dimensions.DimensionsNames[index].Value);
            Assert.Equal(tag.Value, dimensions.DimensionsValues[index].Value);
            index++;
        }

        dimensionsCount += (ushort)(metricPoint.Tags.Count - reservedTags);

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

    private static UserdataV2 GetSerializedData((MetricEventType EventType, byte[] Body) singleEvent)
    {
        var stream = new KaitaiStream(singleEvent.Body);
        var data = new MetricsContract(stream);
        Assert.Equal((ushort)MetricEventType.TLV, data.EventId);
        return data.Body as UserdataV2;
    }

    private static byte[] TransportCopy(byte[] src, int size)
    {
        int headerSize;
        unsafe
        {
            headerSize = sizeof(BinaryHeader);
        }

        var result = new byte[size + headerSize];
        Array.Copy(src, result, size + headerSize);
        return result;
    }

    private class SpyingExporter : BaseExporter<Metric>
    {
        private readonly ICollection<Metric> exportedItems;
        private readonly BaseExporter<Metric> delegatee;

        public SpyingExporter(ICollection<Metric> exportedItems, BaseExporter<Metric> delegatee)
        {
            this.exportedItems = exportedItems;
            this.delegatee = delegatee;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            var result = this.delegatee.Export(batch);
            if (result == ExportResult.Success)
            {
                foreach (var data in batch)
                {
                    this.exportedItems.Add(data);
                }
            }

            return result;
        }

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            return this.delegatee.ForceFlush(timeoutMilliseconds);
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            return this.delegatee.Shutdown(timeoutMilliseconds);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.delegatee.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

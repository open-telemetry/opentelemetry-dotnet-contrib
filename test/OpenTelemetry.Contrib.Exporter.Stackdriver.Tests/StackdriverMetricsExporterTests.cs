// <copyright file="StackdriverMetricsExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using Google.Api;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Moq;
using OpenTelemetry.Contrib.Exporter.Stackdriver.Implementation;
using OpenTelemetry.Metrics.Export;
using Xunit;
using Metric = OpenTelemetry.Metrics.Export.Metric;

namespace OpenTelemetry.Contrib.Exporter.Stackdriver.Tests
{
    public class StackdriverMetricsExporterTests
    {
        private const string MetricName = "<metric-name>";
        private const string ProjectId = "<gcp-project-id>";

        private static readonly DateTime EndTimestamp = DateTime.UtcNow;
        private static readonly DateTime StartTimestamp = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(15));
        private static readonly Mock<StackdriverMetricsExporter> MockStackdriverMetricsExporter =
            new Mock<StackdriverMetricsExporter>(ProjectId);

        [Fact]
        public void ExportAsync_DoubleSum()
        {
            var labelDictionary = new Dictionary<string, string>();
            labelDictionary.Add("<test-key-1>", "<test-value-1>");
            labelDictionary.Add("<test-key-2>", "<test-value-2>");

            TestExportAsync(
                AggregationType.DoubleSum, // open telemetry metric data
                new DoubleSumData()
                {
                    Sum = 5,
                    Labels = labelDictionary.ToList(),
                    StartTimestamp = StartTimestamp,
                    Timestamp = EndTimestamp,
                },
                MetricDescriptor.Types.MetricKind.Cumulative, // expected GCP metric data
                new TypedValue
                {
                    DoubleValue = 5,
                },
                labelDictionary);
        }

        [Fact]
        public void ExportAsync_DoubleSummary()
        {
            TestExportAsync(
                AggregationType.DoubleSummary, // open telemetry metric data
                new DoubleSummaryData()
                {
                    Count = 2,
                    Sum = 4.5,
                    Labels = new List<KeyValuePair<string, string>>(),
                    StartTimestamp = StartTimestamp,
                    Timestamp = EndTimestamp,
                },
                MetricDescriptor.Types.MetricKind.Gauge, // expected GCP metric data
                new TypedValue
                {
                    DoubleValue = 2.25,
                });
        }

        [Fact]
        public void ExportAsync_Int64Summary()
        {
            TestExportAsync(
                AggregationType.Int64Summary, // open telemetry metric data
                new Int64SummaryData()
                {
                    Count = 2,
                    Sum = 5,
                    Labels = new List<KeyValuePair<string, string>>(),
                    StartTimestamp = StartTimestamp,
                    Timestamp = EndTimestamp,
                },
                MetricDescriptor.Types.MetricKind.Gauge, // expected GCP metric data
                new TypedValue
                {
                    DoubleValue = 2.5,
                });
        }

        [Fact]
        public void ExportAsync_LongSum()
        {
            TestExportAsync(
                AggregationType.LongSum, // open telemetry metric data
                new Int64SumData
                {
                    Sum = 15,
                    Labels = new List<KeyValuePair<string, string>>(),
                    StartTimestamp = StartTimestamp,
                    Timestamp = EndTimestamp,
                },
                MetricDescriptor.Types.MetricKind.Cumulative, // expected GCP metric data
                new TypedValue
                {
                    Int64Value = 15,
                });
        }

        [Fact]
        public void ExportAsync_DoubleSumNoData()
        {
            TestExportAsyncNoMetrics(
                AggregationType.DoubleSum,
                new DoubleSumData());
        }

        [Fact]
        public void ExportAsync_DoubleSummaryNoData()
        {
            TestExportAsyncNoMetrics(
                AggregationType.DoubleSummary,
                new DoubleSummaryData());
        }

        [Fact]
        public void ExportAsync_Int64SummaryNoData()
        {
            TestExportAsyncNoMetrics(
                AggregationType.Int64Summary,
                new Int64SummaryData());
        }

        [Fact]
        public void ExportAsync_LongSumNoData()
        {
            TestExportAsyncNoMetrics(
                AggregationType.LongSum,
                new Int64SumData());
        }

        private static void TestExportAsync(
            AggregationType aggregationType,
            MetricData metricData,
            MetricDescriptor.Types.MetricKind expectedMetricKind,
            TypedValue expectedTypedValue,
            IDictionary<string, string> expectedLabels = null)
        {
            MockStackdriverMetricsExporter.CallBase = true;
            var metric = new Metric("<metric-namespace>", MetricName, "<metric-description>", aggregationType);
            metric.Data.Add(metricData);
            var expectedMonitoredResource = GoogleCloudResourceUtils.GetDefaultResource(ProjectId);
            var expectedGoogleMetric = new Google.Api.Metric
            {
                Type = $"custom.googleapis.com/server/{MetricName}",
            };
            if (expectedLabels != null)
            {
                expectedGoogleMetric.Labels.Add(expectedLabels);
            }

            var expectedTimeSeries = new TimeSeries
            {
                Metric = expectedGoogleMetric,
                Resource = expectedMonitoredResource,
                MetricKind = expectedMetricKind,
            };
            var expectedTimeInterval = new TimeInterval
            {
                StartTime = expectedMetricKind == MetricDescriptor.Types.MetricKind.Cumulative
                    ? Timestamp.FromDateTimeOffset(StartTimestamp)
                    : null,
                EndTime = Timestamp.FromDateTimeOffset(EndTimestamp),
            };
            MockStackdriverMetricsExporter
                .Setup(sm =>
                    sm.UploadToGoogleCloudMonitoring(expectedTimeSeries, expectedTypedValue, expectedTimeInterval))
                .Verifiable();

            MockStackdriverMetricsExporter.Object.ExportAsync(new List<Metric> { metric }, CancellationToken.None);

            MockStackdriverMetricsExporter.Verify();
        }

        private static void TestExportAsyncNoMetrics(
            AggregationType aggregationType,
            MetricData metricData)
        {
            MockStackdriverMetricsExporter.CallBase = true;
            var metric = new Metric("<metric-namespace>", "<metric-name>", "<metric-description>", aggregationType);
            metric.Data.Add(metricData);
            MockStackdriverMetricsExporter
                .Setup(cm =>
                    cm.UploadToGoogleCloudMonitoring(It.IsAny<TimeSeries>(), It.IsAny<TypedValue>(), It.IsAny<TimeInterval>()))
                .Throws(new Exception("UploadToGoogleCloudMonitoring should not be called."));

            MockStackdriverMetricsExporter.Object.ExportAsync(new List<Metric> { metric }, CancellationToken.None);

            MockStackdriverMetricsExporter.Verify();
        }
    }
}

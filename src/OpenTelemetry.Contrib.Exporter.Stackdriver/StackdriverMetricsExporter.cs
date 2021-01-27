// <copyright file="StackdriverMetricsExporter.cs" company="OpenTelemetry Authors">
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Google.Api;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Testing;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenTelemetry.Contrib.Exporter.Stackdriver.Implementation;
using OpenTelemetry.Metrics.Export;
using Metric = OpenTelemetry.Metrics.Export.Metric;

namespace OpenTelemetry.Contrib.Exporter.Stackdriver
{
    /// <summary>
    /// Exports a group of metrics to Stackdriver. Supports AggregationTypes:
    /// <see cref="AggregationType.DoubleSum"/>,
    /// <see cref="AggregationType.DoubleSummary"/>,
    /// <see cref="AggregationType.Int64Summary"/>,
    /// <see cref="AggregationType.LongSum"/>.
    /// </summary>
    public class StackdriverMetricsExporter : MetricExporter
    {
        private static readonly string StackdriverExportVersion;
        private static readonly string OpenTelemetryExporterVersion;

        private readonly MetricServiceClient metricServiceClient;
        private readonly ProjectName projectName;
        private readonly MonitoredResource monitoredResource;

        static StackdriverMetricsExporter()
        {
            try
            {
                var assemblyPackageVersion = typeof(StackdriverMetricsExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
                StackdriverExportVersion = assemblyPackageVersion;
            }
            catch (Exception)
            {
                StackdriverExportVersion = $"{Constants.PackagVersionUndefined}";
            }

            try
            {
                OpenTelemetryExporterVersion = Assembly.GetCallingAssembly().GetName().Version.ToString();
            }
            catch (Exception)
            {
                OpenTelemetryExporterVersion = $"{Constants.PackagVersionUndefined}";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StackdriverMetricsExporter"/> class with default monitored
        /// resource.
        /// </summary>
        /// <param name="projectId">Project ID to send telemetry to.</param>
        public StackdriverMetricsExporter(string projectId)
            : this(projectId, GoogleCloudResourceUtils.GetDefaultResource(projectId))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StackdriverMetricsExporter"/> class.
        /// </summary>
        /// <param name="projectId">Project ID to send telemetry to.</param>
        /// <param name="monitoredResource">Monitored resource to associate metric telemetry with.</param>
        public StackdriverMetricsExporter(string projectId, MonitoredResource monitoredResource)
        {
            this.monitoredResource = monitoredResource;
            this.projectName = new ProjectName(projectId);

            // Set header mutation for every outgoing API call to Stackdriver so the BE knows
            // which version of OC client is calling it as well as which version of the exporter
            var metricServiceSettings = new MetricServiceSettings
            {
                CallSettings = CallSettings.FromHeaderMutation(StackdriverCallHeaderAppender),
            };
            this.metricServiceClient = new MetricServiceClientBuilder { Settings = metricServiceSettings }.Build();
        }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            var enumerator = metrics.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;

                if (current == null)
                {
                    break;
                }

                foreach (var metricData in current.Data)
                {
                    // No count information is available except in the type specific metricData, so "no metric data"
                    // case is handled in the methods which convert the metrics and send the data to GCP
                    switch (current.AggregationType)
                    {
                        case AggregationType.DoubleSum:
                            {
                                this.WriteSum(
                                    this.GenerateNewTimeSeries(
                                        current.MetricName,
                                        metricData.Labels,
                                        MetricDescriptor.Types.MetricKind.Cumulative),
                                    (DoubleSumData)metricData);
                                break;
                            }

                        case AggregationType.DoubleSummary:
                            {
                                this.WriteSummary(
                                    this.GenerateNewTimeSeries(
                                        current.MetricName,
                                        metricData.Labels,
                                        MetricDescriptor.Types.MetricKind.Gauge),
                                    (DoubleSummaryData)metricData);
                                break;
                            }

                        case AggregationType.Int64Summary:
                            {
                                this.WriteSummary(
                                    this.GenerateNewTimeSeries(
                                        current.MetricName,
                                        metricData.Labels,
                                        MetricDescriptor.Types.MetricKind.Gauge),
                                    (Int64SummaryData)metricData);
                                break;
                            }

                        case AggregationType.LongSum:
                            {
                                this.WriteSum(
                                    this.GenerateNewTimeSeries(
                                        current.MetricName,
                                        metricData.Labels,
                                        MetricDescriptor.Types.MetricKind.Cumulative),
                                    (Int64SumData)metricData);
                                break;
                            }

                        default:
                            throw new NotSupportedException(
                                $"Unsupported aggregation type: {current.AggregationType}");
                    }
                }
            }

            enumerator.Dispose();

            return Task.FromResult(ExportResult.Success);
        }

        /// <summary>
        /// For testing purposes only. Adds point to time series and uploads to Stackdriver.
        /// </summary>
        /// <param name="timeSeries">Time series containing metric definition.</param>
        /// <param name="value">Metric value.</param>
        /// <param name="timeInterval">Time interval for metric value.</param>
        [VisibleForTestOnly]
        public virtual void UploadToGoogleCloudMonitoring(TimeSeries timeSeries, TypedValue value, TimeInterval timeInterval)
        {
            timeSeries.Points.Add(
                new Point
                {
                    Value = value,
                    Interval = timeInterval,
                });
            this.metricServiceClient.CreateTimeSeries(this.projectName, new[] { timeSeries });

            // TODO: Handle failures
        }

        /// <summary>
        /// Appends OpenTelemetry headers for every outgoing request to Stackdriver Backend.
        /// </summary>
        /// <param name="metadata">The metadata that is sent with every outgoing http request.</param>
        private static void StackdriverCallHeaderAppender(Metadata metadata)
        {
            metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
            metadata.Add("AGENT_LABEL_VALUE_STRING", $"{OpenTelemetryExporterVersion}; stackdriver-exporter {StackdriverExportVersion}");
        }

        private static TimeInterval GetCumulativeInterval(MetricData metricData)
        {
            return new TimeInterval
            {
                StartTime = Timestamp.FromDateTimeOffset(metricData.StartTimestamp),
                EndTime = Timestamp.FromDateTimeOffset(metricData.Timestamp),
            };
        }

        private TimeSeries GenerateNewTimeSeries(
            string metricName,
            IEnumerable<KeyValuePair<string, string>> labelSet,
            MetricDescriptor.Types.MetricKind metricKind)
        {
            var googleMetric = new Google.Api.Metric
            {
                Type = $"custom.googleapis.com/server/{metricName}",
            };
            if (labelSet != null)
            {
                googleMetric.Labels.Add(
                    labelSet.ToDictionary(x => x.Key, x => x.Value));
            }

            return new TimeSeries
            {
                Metric = googleMetric,
                Resource = this.monitoredResource,
                MetricKind = metricKind,
            };
        }

        private void WriteSum(TimeSeries timeSeries, Int64SumData int64SumData)
        {
            if (int64SumData.Sum == 0)
            {
                return;
            }

            var value = new TypedValue
            {
                Int64Value = int64SumData.Sum,
            };
            var timeInterval = GetCumulativeInterval(int64SumData);

            this.UploadToGoogleCloudMonitoring(timeSeries, value, timeInterval);
        }

        private void WriteSum(TimeSeries timeSeries, DoubleSumData doubleSumData)
        {
            if (doubleSumData.Sum == 0)
            {
                return;
            }

            var value = new TypedValue
            {
                DoubleValue = doubleSumData.Sum,
            };
            var timeInterval = GetCumulativeInterval(doubleSumData);

            this.UploadToGoogleCloudMonitoring(timeSeries, value, timeInterval);
        }

        private void WriteSummary(TimeSeries timeSeries, double sum, long count, DateTime timestamp)
        {
            if (count == 0)
            {
                return;
            }

            var value = new TypedValue
            {
                DoubleValue = sum / count,
            };
            var timeInterval = new TimeInterval
            {
                EndTime = Timestamp.FromDateTimeOffset(timestamp),
            };

            this.UploadToGoogleCloudMonitoring(timeSeries, value, timeInterval);
        }

        private void WriteSummary(TimeSeries timeSeries, Int64SummaryData int64SummaryData)
        {
            this.WriteSummary(timeSeries, int64SummaryData.Sum, int64SummaryData.Count, int64SummaryData.Timestamp);
        }

        private void WriteSummary(TimeSeries timeSeries, DoubleSummaryData doubleSummaryData)
        {
            this.WriteSummary(timeSeries, doubleSummaryData.Sum, doubleSummaryData.Count, doubleSummaryData.Timestamp);
        }
    }
}

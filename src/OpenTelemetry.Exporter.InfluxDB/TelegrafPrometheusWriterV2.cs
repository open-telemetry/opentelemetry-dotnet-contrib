// <copyright file="TelegrafPrometheusWriterV2.cs" company="OpenTelemetry Authors">
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

using System.Globalization;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class TelegrafPrometheusWriterV2 : IMetricsWriter
{
    public void Write(Metric metric, Resource? resource, WriteApi writeApi)
    {
        var measurement = "prometheus";
        var metricName = metric.Name;

        switch (metric.MetricType)
        {
            case MetricType.LongGauge:
                {
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, metricPoint.GetGaugeLastValueLong())
                            .Tags(metricPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(metricPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.DoubleGauge:
                {
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, metricPoint.GetGaugeLastValueDouble())
                            .Tags(metricPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(metricPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.LongSum:
                {
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, metricPoint.GetSumLong())
                            .Tags(metricPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(metricPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.DoubleSum:
                {
                    foreach (var metricPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, metricPoint.GetSumDouble())
                            .Tags(metricPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(metricPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.LongSumNonMonotonic:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, dataPoint.GetSumLong())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.DoubleSumNonMonotonic:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field(metricName, dataPoint.GetSumDouble())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.Histogram:
                foreach (var metricPoint in metric.GetMetricPoints())
                {
                    var basePoint = PointData
                        .Measurement(measurement)
                        .Tags(metricPoint.Tags)
                        .Tags(resource?.Attributes)
                        .Timestamp(metricPoint.EndTime.UtcDateTime, WritePrecision.Ns);

                    var headPoint = basePoint
                        .Field($"{metricName}_count", metricPoint.GetHistogramCount())
                        .Field($"{metricName}_sum", metricPoint.GetHistogramSum());

                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        headPoint = headPoint
                            .Field($"{metricName}_min", min)
                            .Field($"{metricName}_max", max);
                    }

                    writeApi.WritePoint(headPoint);

                    foreach (var histogramBucket in metricPoint.GetHistogramBuckets())
                    {
                        var boundFieldKey = double.IsPositiveInfinity(histogramBucket.ExplicitBound)
                            ? "+Inf"
                            : histogramBucket.ExplicitBound.ToString("F", CultureInfo.InvariantCulture);
                        var bucketPoint = basePoint
                            .Tag("le", boundFieldKey)
                            .Field($"{metricName}_bucket", histogramBucket.BucketCount);
                        writeApi.WritePoint(bucketPoint);
                    }
                }

                break;
        }
    }
}

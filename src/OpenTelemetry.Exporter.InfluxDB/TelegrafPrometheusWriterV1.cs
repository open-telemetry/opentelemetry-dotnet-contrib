// <copyright file="TelegrafPrometheusWriterV1.cs" company="OpenTelemetry Authors">
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

internal sealed class TelegrafPrometheusWriterV1 : IMetricsWriter
{
    public void Write(Metric metric, Resource? resource, WriteApi writeApi)
    {
        var measurement = metric.Name;

        switch (metric.MetricType)
        {
            case MetricType.LongGauge:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field("gauge", dataPoint.GetGaugeLastValueLong())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.DoubleGauge:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field("gauge", dataPoint.GetGaugeLastValueDouble())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.LongSum:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field("counter", dataPoint.GetSumLong())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.DoubleSum:
                {
                    foreach (var dataPoint in metric.GetMetricPoints())
                    {
                        var pointData = PointData
                            .Measurement(measurement)
                            .Field("counter", dataPoint.GetSumDouble())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
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
                            .Field("gauge", dataPoint.GetSumLong())
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
                            .Field("gauge", dataPoint.GetSumDouble())
                            .Tags(dataPoint.Tags)
                            .Tags(resource?.Attributes)
                            .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);
                        writeApi.WritePoint(pointData);
                    }

                    break;
                }

            case MetricType.Histogram:
                foreach (var dataPoint in metric.GetMetricPoints())
                {
                    var pointData = PointData
                        .Measurement(measurement)
                        .Field("count", dataPoint.GetHistogramCount())
                        .Field("sum", dataPoint.GetHistogramSum())
                        .Tags(dataPoint.Tags)
                        .Tags(resource?.Attributes)
                        .Timestamp(dataPoint.EndTime.UtcDateTime, WritePrecision.Ns);

                    if (dataPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        pointData = pointData.Field("min", min)
                            .Field("max", max);
                    }

                    foreach (var histogramBucket in dataPoint.GetHistogramBuckets())
                    {
                        var boundFieldKey = double.IsPositiveInfinity(histogramBucket.ExplicitBound)
                            ? "+Inf"
                            : histogramBucket.ExplicitBound.ToString("F", CultureInfo.InvariantCulture);
                        pointData = pointData.Field(boundFieldKey, histogramBucket.BucketCount);
                    }

                    writeApi.WritePoint(pointData);
                }

                break;
        }
    }
}

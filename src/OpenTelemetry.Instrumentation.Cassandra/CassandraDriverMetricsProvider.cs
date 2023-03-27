// <copyright file="CassandraDriverMetricsProvider.cs" company="OpenTelemetry Authors">
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
using Cassandra.Metrics;
using Cassandra.Metrics.Abstractions;

namespace OpenTelemetry.Instrumentation.Cassandra;

internal sealed class CassandraDriverMetricsProvider : IDriverMetricsProvider
{
    private const string Prefix = "cassandra";

    public IDriverTimer Timer(string bucket, IMetric metric)
    {
        return new DriverTimer($"{Prefix}.{metric.Name}");
    }

    public IDriverMeter Meter(string bucket, IMetric metric)
    {
        return new DriverMeter($"{Prefix}.{metric.Name}");
    }

    public IDriverCounter Counter(string bucket, IMetric metric)
    {
        return new DriverCounter($"{Prefix}.{metric.Name}");
    }

    public IDriverGauge Gauge(string bucket, IMetric metric, Func<double?> valueProvider)
    {
        return new DriverGauge($"{Prefix}.{metric.Name}", Value(valueProvider));
    }

    public void ShutdownMetricsBucket(string bucket)
    {
    }

    private static Func<double> Value(Func<double?> valueProvider)
    {
        return () => valueProvider.Invoke() ?? 0;
    }
}

// <copyright file="CassandraBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using Cassandra;
using Cassandra.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Cassandra;

/// <summary>
/// Extension methods to simplify registering cassandra driver metrics.
/// </summary>
public static class CassandraBuilderExtensions
{
    /// <summary>
    /// Configuring open telemetry metrics for cassandra.
    /// </summary>
    /// <param name="builder">Cassandra builder.</param>
    /// <returns>Returning Cassandra builder.</returns>
    public static Builder WithOpenTelemetryMetrics(this Builder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.WithMetrics(new CassandraDriverMetricsProvider(), GetDefaultOptions());
    }

    /// <summary>
    /// Configuring open telemetry metrics for cassandra.
    /// </summary>
    /// <param name="builder">Cassandra builder.</param>
    /// <param name="options">Cassandra driver metrics options.</param>
    /// <returns>Returning Cassandra builder.</returns>
    public static Builder WithOpenTelemetryMetrics(this Builder builder, DriverMetricsOptions? options)
    {
        Guard.ThrowIfNull(builder);

        return builder.WithMetrics(new CassandraDriverMetricsProvider(), options ?? GetDefaultOptions());
    }

    private static DriverMetricsOptions GetDefaultOptions()
    {
        var options = new DriverMetricsOptions();

        options.SetEnabledNodeMetrics(NodeMetric.AllNodeMetrics);
        options.SetEnabledSessionMetrics(SessionMetric.AllSessionMetrics);

        return options;
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Cassandra.Metrics.Abstractions;

namespace OpenTelemetry.Instrumentation.Cassandra;

internal sealed class DriverGauge : IDriverGauge
{
    private readonly ObservableGauge<double> gauge;

    public DriverGauge(string name, Func<double> value)
    {
        this.gauge = CassandraMeter.Instance.CreateObservableGauge(name, value);
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.Cassandra;

internal static class CassandraMeter
{
    public static Meter Instance { get; } = Metrics.MeterFactory.Create(typeof(CassandraMeter), null); // These metrics are not in the Semantic Conventions
}

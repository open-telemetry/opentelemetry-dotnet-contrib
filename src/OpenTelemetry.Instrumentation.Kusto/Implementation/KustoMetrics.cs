// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Holds the Meter and instruments used to emit Kusto metrics.
/// </summary>
internal static class KustoMetrics
{
    public static readonly Meter Meter = Metrics.MeterFactory.Create(typeof(KustoMetrics), KustoSemanticConventions.SemanticConventionsVersion);

    public static readonly string MeterName = Meter.Name;

    public static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        advice: new InstrumentAdvice<double>() { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] },
        description: "Duration of database client operations");
}

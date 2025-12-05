// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Helper class to hold common properties used by Kusto instrumentation.
/// </summary>
internal static class KustoActivitySourceHelper
{
    public const string DbSystem = "azure.kusto";
    public const string ActivitySourceName = "Kusto.Client";
    public const string MeterName = "Kusto.Client";
    public const string ClientRequestIdTagKey = "kusto.client_request_id";

    public static readonly Assembly Assembly = typeof(KustoActivitySourceHelper).Assembly;
    public static readonly string PackageVersion = Assembly.GetPackageVersion();
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, PackageVersion);
    public static readonly Meter Meter = new(MeterName, PackageVersion);

    public static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        advice: new InstrumentAdvice<double>() { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] },
        description: "Duration of database client operations");

    public static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
        "db.client.operation.count",
        description: "Number of database client operations");
}

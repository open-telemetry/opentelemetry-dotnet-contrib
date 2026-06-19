// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Helper class to hold common properties used by Kusto instrumentation.
/// </summary>
internal static class KustoActivitySourceHelper
{
    public const string DbSystemNameValue = "azure.kusto";
    public const string ClientRequestIdTagKey = $"{DbSystemNameValue}.client_request_id";

    public static readonly Version SemanticConventionsVersion = new(1, 33, 0);

    public static readonly Assembly Assembly = typeof(KustoActivitySourceHelper).Assembly;
    public static readonly AssemblyName AssemblyName = Assembly.GetName();

    public static readonly string ActivitySourceName = AssemblyName.Name!;
    public static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create(typeof(KustoActivitySourceHelper), SemanticConventionsVersion);

    public static readonly string MeterName = AssemblyName.Name!;
    public static readonly Meter Meter = Metrics.MeterFactory.Create(typeof(KustoActivitySourceHelper), SemanticConventionsVersion);

    public static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        advice: new InstrumentAdvice<double>() { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] },
        description: "Duration of database client operations");
}

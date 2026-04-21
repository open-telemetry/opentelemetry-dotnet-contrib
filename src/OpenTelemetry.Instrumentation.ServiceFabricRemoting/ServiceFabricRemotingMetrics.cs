// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingMetrics
{
    internal const string MetricNameRpcServerCallDuration = "rpc.server.call.duration";
    internal const string MetricNameRpcClientCallDuration = "rpc.client.call.duration";
    internal const string MetricUnitSeconds = "s";
    internal const string MetricDescriptionRpcServerCallDuration = "Measures the duration of an incoming Remote Procedure Call (RPC).";
    internal const string MetricDescriptionRpcClientCallDuration = "Measures the duration of an outgoing Remote Procedure Call (RPC).";

    // Per-spec recommended explicit bucket boundaries (seconds).
    internal static readonly double[] DurationHistogramBucketBoundaries = new double[]
    {
        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10,
    };

    // Meter name matches the ActivitySource name (assembly name) by convention.
    internal static readonly string MeterName = ServiceFabricRemotingActivitySource.ActivitySourceName;

    public static readonly Meter Meter = new(MeterName, ServiceFabricRemotingActivitySource.Assembly.GetPackageVersion());

    public static readonly Histogram<double> ServerCallDuration = Meter.CreateHistogram<double>(
        name: MetricNameRpcServerCallDuration,
        unit: MetricUnitSeconds,
        description: MetricDescriptionRpcServerCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = DurationHistogramBucketBoundaries,
        });

    public static readonly Histogram<double> ClientCallDuration = Meter.CreateHistogram<double>(
        name: MetricNameRpcClientCallDuration,
        unit: MetricUnitSeconds,
        description: MetricDescriptionRpcClientCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = DurationHistogramBucketBoundaries,
        });
}

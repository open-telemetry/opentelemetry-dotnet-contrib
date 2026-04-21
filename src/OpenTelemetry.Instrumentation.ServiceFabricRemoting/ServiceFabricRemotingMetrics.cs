// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// Metric instruments emitted by the ServiceFabricRemoting instrumentation.
/// The <see cref="MeterName"/> constant is exposed for consumers that want to
/// register the meter manually with a <c>MeterProviderBuilder</c> or a
/// <c>MeterListener</c>.
/// </summary>
public static class ServiceFabricRemotingMetrics
{
    /// <summary>
    /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> used by this instrumentation.
    /// </summary>
    public const string MeterName = "OpenTelemetry.Instrumentation.ServiceFabricRemoting";

    internal static readonly Meter Meter = new(MeterName, typeof(ServiceFabricRemotingMetrics).Assembly.GetPackageVersion());

    internal static readonly Histogram<double> ServerCallDuration = Meter.CreateHistogram<double>(
        name: ServiceFabricRemotingSemanticConventions.MetricNameRpcServerCallDuration,
        unit: ServiceFabricRemotingSemanticConventions.MetricUnitSeconds,
        description: ServiceFabricRemotingSemanticConventions.MetricDescriptionRpcServerCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = ServiceFabricRemotingSemanticConventions.DurationHistogramBucketBoundaries,
        });

    internal static readonly Histogram<double> ClientCallDuration = Meter.CreateHistogram<double>(
        name: ServiceFabricRemotingSemanticConventions.MetricNameRpcClientCallDuration,
        unit: ServiceFabricRemotingSemanticConventions.MetricUnitSeconds,
        description: ServiceFabricRemotingSemanticConventions.MetricDescriptionRpcClientCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = ServiceFabricRemotingSemanticConventions.DurationHistogramBucketBoundaries,
        });
}

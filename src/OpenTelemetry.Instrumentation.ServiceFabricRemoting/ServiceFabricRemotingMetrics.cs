// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingMetrics
{
    internal static readonly Meter Meter = new(typeof(ServiceFabricRemotingMetrics).Assembly.GetName().Name!, typeof(ServiceFabricRemotingMetrics).Assembly.GetPackageVersion());

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

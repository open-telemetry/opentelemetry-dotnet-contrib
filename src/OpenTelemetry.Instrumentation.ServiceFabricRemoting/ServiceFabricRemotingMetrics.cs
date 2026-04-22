// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingMetrics
{
    // Version of the OpenTelemetry RPC semantic conventions this instrumentation targets.
    // Used for the Meter's TelemetrySchemaUrl (see #4064).
    internal static readonly Version SemanticConventionsVersion = new(1, 40, 0);

    internal static readonly Meter Meter = MeterFactory.Create(
        typeof(ServiceFabricRemotingMetrics),
        SemanticConventionsVersion);

    internal static readonly Histogram<double> ServerCallDuration = Meter.CreateHistogram(
        name: ServiceFabricRemotingSemanticConventions.MetricNameRpcServerCallDuration,
        unit: ServiceFabricRemotingSemanticConventions.MetricUnitSeconds,
        description: ServiceFabricRemotingSemanticConventions.MetricDescriptionRpcServerCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = ServiceFabricRemotingSemanticConventions.DurationHistogramBucketBoundaries,
        });

    internal static readonly Histogram<double> ClientCallDuration = Meter.CreateHistogram(
        name: ServiceFabricRemotingSemanticConventions.MetricNameRpcClientCallDuration,
        unit: ServiceFabricRemotingSemanticConventions.MetricUnitSeconds,
        description: ServiceFabricRemotingSemanticConventions.MetricDescriptionRpcClientCallDuration,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = ServiceFabricRemotingSemanticConventions.DurationHistogramBucketBoundaries,
        });
}

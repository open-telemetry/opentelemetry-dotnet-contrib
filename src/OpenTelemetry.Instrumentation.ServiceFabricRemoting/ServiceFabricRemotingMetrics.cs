// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingMetrics
{
    // Custom value for rpc.system.name; the OpenTelemetry RPC semantic conventions
    // explicitly allow custom values when none of the well-known enum entries applies.
    // https://opentelemetry.io/docs/specs/semconv/rpc/
    internal const string RpcSystemServiceFabricRemoting = "service_fabric_remoting";

    // Version of the OpenTelemetry RPC semantic conventions this instrumentation targets.
    // Used for the Meter's TelemetrySchemaUrl (see #4064).
    internal static readonly Version SemanticConventionsVersion = new(1, 40, 0);

    // Per-spec recommended explicit bucket boundaries for RPC duration histograms (seconds).
    internal static readonly double[] DurationHistogramBucketBoundaries = new double[]
    {
        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10,
    };

    internal static readonly Meter Meter = MeterFactory.Create(
        typeof(ServiceFabricRemotingMetrics),
        SemanticConventionsVersion);

    internal static readonly Histogram<double> ServerCallDuration = Meter.CreateHistogram(
        name: "rpc.server.call.duration",
        unit: "s",
        description: "Measures the duration of an incoming Remote Procedure Call (RPC).",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = DurationHistogramBucketBoundaries,
        });

    internal static readonly Histogram<double> ClientCallDuration = Meter.CreateHistogram(
        name: "rpc.client.call.duration",
        unit: "s",
        description: "Measures the duration of an outgoing Remote Procedure Call (RPC).",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = DurationHistogramBucketBoundaries,
        });
}

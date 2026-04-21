// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingSemanticConventions
{
    // Custom value for rpc.system.name; the OpenTelemetry RPC semantic conventions
    // explicitly allow custom values when none of the well-known enum entries applies.
    // https://opentelemetry.io/docs/specs/semconv/rpc/
    internal const string RpcSystemServiceFabricRemoting = "service_fabric_remoting";

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
}

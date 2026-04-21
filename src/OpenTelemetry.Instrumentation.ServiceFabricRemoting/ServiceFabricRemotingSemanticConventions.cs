// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal static class ServiceFabricRemotingSemanticConventions
{
    // Custom value for rpc.system.name; the OpenTelemetry RPC semantic conventions
    // explicitly allow custom values when none of the well-known enum entries applies.
    // https://opentelemetry.io/docs/specs/semconv/rpc/
    internal const string RpcSystemServiceFabricRemoting = "service_fabric_remoting";
}

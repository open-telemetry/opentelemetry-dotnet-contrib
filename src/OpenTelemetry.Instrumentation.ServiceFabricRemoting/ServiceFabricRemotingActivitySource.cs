// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal class ServiceFabricRemotingActivitySource
{
    // Version of the OpenTelemetry RPC semantic conventions this instrumentation targets for tracing.
    internal static readonly Version SemanticConventionsVersion = new(1, 40, 0);

    internal static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create(
        typeof(ServiceFabricRemotingActivitySource),
        SemanticConventionsVersion);

    internal static readonly string IncomingRequestActivityName = ActivitySource.Name + ".IncomingRequest";
    internal static readonly string OutgoingRequestActivityName = ActivitySource.Name + ".OutgoingRequest";

    internal static ServiceFabricRemotingInstrumentationOptions? Options { get; set; }
}

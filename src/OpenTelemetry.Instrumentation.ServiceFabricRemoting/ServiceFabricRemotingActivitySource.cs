// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

internal class ServiceFabricRemotingActivitySource
{
    internal static readonly Assembly Assembly = typeof(ServiceFabricRemotingActivitySource).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
#pragma warning disable IDE0370 // Suppression is unnecessary
    internal static readonly string ActivitySourceName = AssemblyName.Name!;
#pragma warning restore IDE0370 // Suppression is unnecessary

    internal static readonly string IncomingRequestActivityName = ActivitySourceName + ".IncomingRequest";
    internal static readonly string OutgoingRequestActivityName = ActivitySourceName + ".OutgoingRequest";

    // TODO https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/4064 add the appropriate SemConv version
    public static ActivitySource ActivitySource { get; } = Trace.ActivitySourceFactory.Create<ServiceFabricRemotingActivitySource>(null);

    public static ServiceFabricRemotingInstrumentationOptions? Options { get; set; }
}

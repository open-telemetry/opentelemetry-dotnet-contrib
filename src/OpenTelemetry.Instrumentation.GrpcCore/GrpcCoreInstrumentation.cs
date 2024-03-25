// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// Instrumentation class Grpc.Core.
/// </summary>
internal static class GrpcCoreInstrumentation
{
    /// <summary>
    /// The assembly name.
    /// </summary>
    internal static readonly AssemblyName AssemblyName = typeof(GrpcCoreInstrumentation).Assembly.GetName();

    /// <summary>
    /// The activity source name.
    /// </summary>
    internal static readonly string ActivitySourceName = AssemblyName.Name;

    /// <summary>
    /// The activity source.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, ActivitySourceVersionHelper.GetVersion<ServerTracingInterceptorOptions>());
}

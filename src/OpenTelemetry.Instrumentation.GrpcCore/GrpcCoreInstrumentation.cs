// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
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
    /// The version.
    /// </summary>
    internal static readonly Version Version = AssemblyName.Version;

    /// <summary>
    /// The activity source.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
}

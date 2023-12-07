// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.Owin;

internal static class OwinInstrumentationActivitySource
{
    internal static readonly AssemblyName AssemblyName = typeof(OwinInstrumentationActivitySource).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly string IncomingRequestActivityName = ActivitySourceName + ".IncomingRequest";

    private static readonly Version Version = AssemblyName.Version;

    public static ActivitySource ActivitySource { get; } = new ActivitySource(ActivitySourceName, Version.ToString());

    public static OwinInstrumentationOptions Options { get; set; }
}

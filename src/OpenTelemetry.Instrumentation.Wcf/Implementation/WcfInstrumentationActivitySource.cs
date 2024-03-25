// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel.Channels;
using OpenTelemetry.Instrumentation.Wcf.Implementation;

namespace OpenTelemetry.Instrumentation.Wcf;

/// <summary>
/// WCF instrumentation.
/// </summary>
internal static class WcfInstrumentationActivitySource
{
    internal static readonly AssemblyName AssemblyName = typeof(WcfInstrumentationActivitySource).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly string IncomingRequestActivityName = ActivitySourceName + ".IncomingRequest";
    internal static readonly string OutgoingRequestActivityName = ActivitySourceName + ".OutgoingRequest";

    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, ActivitySourceVersionHelper.GetVersion<WcfInstrumentationOptions>());

    public static WcfInstrumentationOptions? Options { get; set; }

    public static IEnumerable<string>? MessageHeaderValuesGetter(Message request, string name)
    {
        return TelemetryPropagationReader.Default(request, name);
    }

    public static void MessageHeaderValueSetter(Message request, string name, string value)
    {
        TelemetryPropagationWriter.Default(request, name, value);
    }
}

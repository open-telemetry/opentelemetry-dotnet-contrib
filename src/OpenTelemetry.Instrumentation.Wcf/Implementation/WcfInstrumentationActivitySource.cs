// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel.Channels;
using OpenTelemetry.Instrumentation.Wcf.Implementation;

namespace OpenTelemetry.Instrumentation.Wcf;

/// <summary>
/// WCF instrumentation.
/// </summary>
internal static class WcfInstrumentationActivitySource
{
    internal static readonly Version SemanticConventionsVersion = new(1, 41, 0);
    internal static readonly ActivitySource ActivitySource = Trace.ActivitySourceFactory.Create(typeof(WcfInstrumentationActivitySource), SemanticConventionsVersion);
    internal static readonly string IncomingRequestActivityName = ActivitySource.Name + ".IncomingRequest";
    internal static readonly string OutgoingRequestActivityName = ActivitySource.Name + ".OutgoingRequest";
    internal static readonly string UnassociatedExceptionActivityName = ActivitySource.Name + ".Exception";

    public static WcfInstrumentationOptions? Options { get; set; }

    public static IEnumerable<string>? MessageHeaderValuesGetter(Message request, string name)
        => TelemetryPropagationReader.Default(request, name);

    public static void MessageHeaderValueSetter(Message request, string name, string value)
        => TelemetryPropagationWriter.Default(request, name, value);
}

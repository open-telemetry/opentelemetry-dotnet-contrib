// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Owin.Implementation;

internal static class OwinInstrumentationMetrics
{
    internal static readonly Assembly Assembly = typeof(OwinInstrumentationMetrics).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();

    public static string MeterName => AssemblyName.Name;

    public static Meter Instance => new(MeterName, Assembly.GetPackageVersion());

    public static Histogram<double> HttpServerDuration => Instance.CreateHistogram<double>("http.server.request.duration", "s", "Duration of HTTP server requests.");
}

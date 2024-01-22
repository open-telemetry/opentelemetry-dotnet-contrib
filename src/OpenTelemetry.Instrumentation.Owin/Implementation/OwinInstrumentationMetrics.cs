// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.Owin.Implementation;

internal static class OwinInstrumentationMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(OwinInstrumentationMetrics).Assembly.GetName();

    public static string MeterName => AssemblyName.Name;

    public static Meter Instance => new Meter(MeterName, AssemblyName.Version.ToString());

    public static Histogram<double> HttpServerDuration => Instance.CreateHistogram<double>("http.server.request.duration", "s", "Duration of HTTP server requests.");
}

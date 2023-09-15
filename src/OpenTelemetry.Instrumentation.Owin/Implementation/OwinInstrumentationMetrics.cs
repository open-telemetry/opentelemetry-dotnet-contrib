// <copyright file="OwinInstrumentationMetrics.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics.Metrics;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.Owin.Implementation;

internal static class OwinInstrumentationMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(OwinInstrumentationMetrics).Assembly.GetName();

    public static string MeterName => AssemblyName.Name;

    public static Meter Instance => new Meter(MeterName, AssemblyName.Version.ToString());

    public static Histogram<double> HttpServerDuration => Instance.CreateHistogram<double>("http.server.request.duration", "s", "Measures the duration of inbound HTTP requests.");
}

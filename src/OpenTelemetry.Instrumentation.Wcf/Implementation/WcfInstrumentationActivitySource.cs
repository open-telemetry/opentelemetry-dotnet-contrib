// <copyright file="WcfInstrumentationActivitySource.cs" company="OpenTelemetry Authors">
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

using System;
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

    private static readonly Version Version = AssemblyName.Version;

    public static ActivitySource ActivitySource { get; } = new ActivitySource(ActivitySourceName, Version.ToString());

    public static WcfInstrumentationOptions Options { get; set; }

    public static IEnumerable<string> MessageHeaderValuesGetter(Message request, string name)
    {
        return TelemetryPropagationReader.Default(request, name);
    }

    public static void MessageHeaderValueSetter(Message request, string name, string value)
    {
        TelemetryPropagationWriter.Default(request, name, value);
    }
}

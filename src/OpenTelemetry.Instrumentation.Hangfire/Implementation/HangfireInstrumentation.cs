// <copyright file="HangfireInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Reflection;
using Hangfire;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

internal sealed class HangfireInstrumentation
{
    /// <summary>
    /// The assembly name.
    /// </summary>
    internal static readonly AssemblyName AssemblyName = typeof(HangfireInstrumentation).Assembly.GetName();

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
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

    /// <summary>
    /// The default display name delegate.
    /// </summary>
    internal static readonly Func<BackgroundJob, string> DefaultDisplayNameFunc =
        backgroundJob => $"JOB {backgroundJob.Job.Type.Name}.{backgroundJob.Job.Method.Name}";

    public HangfireInstrumentation(HangfireInstrumentationOptions options)
    {
        GlobalJobFilters.Filters.Add(new HangfireInstrumentationJobFilterAttribute(options));
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Hangfire;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for Hangfire jobs instrumentation.
/// </summary>
public class HangfireInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
    /// </summary>
    /// <remarks>
    /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
    /// </remarks>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets or sets a delegate used to format the job name.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>{backgroundJob.Job.Type.Name}.{backgroundJob.Job.Method.Name}</c>.
    /// </remarks>
    public Func<BackgroundJob, string> DisplayNameFunc { get; set; } = HangfireInstrumentation.DefaultDisplayNameFunc;
}

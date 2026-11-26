// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md.
    /// </remarks>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets or sets a delegate used to format the job name.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>{backgroundJob.Job.Type.Name}.{backgroundJob.Job.Method.Name}</c>.
    /// The default is also used when the delegate throws an exception or
    /// returns <see langword="null" />.
    /// </remarks>
    public Func<BackgroundJob, string> DisplayNameFunc { get; set; } = HangfireInstrumentation.DefaultDisplayNameFunc;

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry about the <see cref="BackgroundJob"/> being executed.
    /// </summary>
    /// <remarks>
    /// <b>Notes:</b>
    /// <list type="bullet">
    /// <item>The first parameter passed to the filter function is <see cref="BackgroundJob"/> being executed.</item>
    /// <item>The filter runs before the job activity starts, so
    /// <see cref="System.Diagnostics.Activity.Current"/> is not the job
    /// activity yet.</item>
    /// <item>The return value for the filter:
    /// <list type="number">
    /// <item>If filter returns <see langword="true" />, the command is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception, the command is <b>NOT</b> collected. Activities started
    /// while the job runs inherit the unrecorded trace flags, so the
    /// default sampler (<c>ParentBased(AlwaysOn)</c>) does not collect them
    /// either.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<BackgroundJob, bool>? Filter { get; set; }
}

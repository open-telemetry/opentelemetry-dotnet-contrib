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
    /// <item>The return value for the filter:
    /// <list type="number">
    /// <item>If filter returns <see langword="true" />, the command is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception, the command is <b>NOT</b> collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<BackgroundJob, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to record the pending state duration in metrics.
    /// </summary>
    /// <remarks>
    /// When enabled, records workflow.execution.duration with state="pending", representing
    /// the time jobs spend waiting in the queue before execution starts.
    /// This requires an additional database call per job execution to retrieve the enqueue timestamp.
    /// Default is <see langword="false"/> to avoid performance impact in high-throughput scenarios.
    /// When disabled, only execution duration (state="executing") is recorded.
    /// </remarks>
    public bool RecordQueueLatency { get; set; }
}

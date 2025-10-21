// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Centralized metrics definitions for Hangfire instrumentation.
/// </summary>
internal static class HangfireMetrics
{
    private static readonly Assembly Assembly = typeof(HangfireMetrics).Assembly;
    private static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string MeterName = AssemblyName.Name!;
    private static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    // Metric name constants
    internal const string ExecutionCountMetricName = "workflow.execution.count";
    internal const string ExecutionDurationMetricName = "workflow.execution.duration";
    internal const string ExecutionStatusMetricName = "workflow.execution.status";
    internal const string ExecutionErrorsMetricName = "workflow.execution.errors";

    internal const string QueueLatencyMetricName = "hangfire.queue.latency";

    /// <summary>
    /// The meter instance for all Hangfire metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, InstrumentationVersion);

    /// <summary>
    /// Counter for the number of task executions which have been initiated.
    /// Follows OpenTelemetry workflow semantic conventions.
    /// </summary>
    public static readonly Counter<long> ExecutionCount =
        Meter.CreateCounter<long>(ExecutionCountMetricName, unit: "{executions}",
            description: "The number of task executions which have been initiated.");

    /// <summary>
    /// Histogram for duration of an execution grouped by task, type and result.
    /// Follows OpenTelemetry workflow semantic conventions.
    /// </summary>
    public static readonly Histogram<double> ExecutionDuration =
        Meter.CreateHistogram<double>(ExecutionDurationMetricName, unit: "s",
            description: "Duration of an execution grouped by task, type and result.");

    /// <summary>
    /// UpDownCounter for the number of actively running tasks grouped by task and state.
    /// Follows OpenTelemetry workflow semantic conventions.
    /// </summary>
    public static readonly UpDownCounter<long> ExecutionStatus =
        Meter.CreateUpDownCounter<long>(ExecutionStatusMetricName, unit: "{executions}",
            description: "The number of actively running tasks grouped by task, type and the current state.");

    /// <summary>
    /// Counter for the number of errors encountered in task runs (eg. compile, test failures).
    /// Follows OpenTelemetry workflow semantic conventions.
    /// </summary>
    public static readonly Counter<long> ExecutionErrors =
        Meter.CreateCounter<long>(ExecutionErrorsMetricName, unit: "{error}",
            description: "The number of errors encountered in task runs (eg. compile, test failures).");

    /// <summary>
    /// Histogram for time tasks spend waiting in queue before execution.
    /// Hangfire-specific metric (not part of standard workflow conventions).
    /// </summary>
    public static readonly Histogram<double> QueueLatency =
        Meter.CreateHistogram<double>(QueueLatencyMetricName, unit: "s",
            description: "Time tasks spend waiting in queue before execution starts.");
}

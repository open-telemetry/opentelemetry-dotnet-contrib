// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Hangfire.Common;
using Hangfire.Server;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire filter that records OpenTelemetry metrics for job execution.
/// </summary>
internal sealed class HangfireMetricsJobFilterAttribute : JobFilterAttribute, IServerFilter
{
    private const string StopwatchKey = "OpenTelemetry.Metrics.Stopwatch";

    public void OnPerforming(PerformingContext filterContext)
    {
        filterContext.Items[StopwatchKey] = Stopwatch.StartNew();
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        // Get recurring job ID if this job was triggered by a recurring job
        string? recurringJobId = null;
        try
        {
            recurringJobId = filterContext.Connection.GetJobParameter(
                filterContext.BackgroundJob.Id,
                "RecurringJobId");
        }
        catch
        {
            // If we can't get the recurring job ID, treat it as a non-recurring job
        }

        var backgroundJob = filterContext.BackgroundJob;

        // Record execution outcome (without state attribute per semantic conventions)
        var countTags = HangfireTagBuilder.BuildExecutionCountTags(
            backgroundJob,
            filterContext.Exception);

        HangfireMetrics.ExecutionOutcome.Add(1, countTags);

        // Record execution duration (with state="executing" to differentiate from pending phase)
        if (filterContext.Items.TryGetValue(StopwatchKey, out var stopwatchObj) && stopwatchObj is Stopwatch stopwatch)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;

            var durationTags = HangfireTagBuilder.BuildExecutionTags(
                backgroundJob,
                filterContext.Exception,
                workflowState: WorkflowAttributes.WorkflowStateValues.Executing);

            HangfireMetrics.ExecutionDuration.Record(duration, durationTags);
        }

        // Record workflow-level metrics (includes trigger type)
        var workflowTags = HangfireTagBuilder.BuildWorkflowTags(
            backgroundJob,
            filterContext.Exception,
            recurringJobId);

        HangfireMetrics.WorkflowOutcome.Add(1, workflowTags);
    }
}

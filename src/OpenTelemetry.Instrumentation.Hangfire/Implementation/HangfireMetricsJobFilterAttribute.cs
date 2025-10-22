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

    public void OnPerforming(PerformingContext performingContext)
    {
        performingContext.Items[StopwatchKey] = Stopwatch.StartNew();
    }

    public void OnPerformed(PerformedContext performedContext)
    {
        // Get recurring job ID if this job was triggered by a recurring job
        string? recurringJobId = null;
        try
        {
            recurringJobId = performedContext.Connection.GetJobParameter(
                performedContext.BackgroundJob.Id,
                "RecurringJobId");
        }
        catch
        {
            // If we can't get the recurring job ID, treat it as a non-recurring job
        }

        // Record execution count (without state attribute per semantic conventions)
        var countTags = HangfireTagBuilder.BuildExecutionCountTags(
            performedContext.BackgroundJob.Job,
            performedContext.Exception);

        HangfireMetrics.ExecutionCount.Add(1, countTags);

        // Record execution duration (with state="executing" to differentiate from pending phase)
        if (performedContext.Items.TryGetValue(StopwatchKey, out var stopwatchObj) && stopwatchObj is Stopwatch stopwatch)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;

            var durationTags = HangfireTagBuilder.BuildExecutionTags(
                performedContext.BackgroundJob.Job,
                performedContext.Exception,
                workflowState: HangfireTagBuilder.StateExecuting);

            HangfireMetrics.ExecutionDuration.Record(duration, durationTags);
        }

        // Record workflow-level metrics (includes trigger type)
        var workflowTags = HangfireTagBuilder.BuildWorkflowTags(
            performedContext.BackgroundJob.Job,
            performedContext.Exception,
            recurringJobId);

        HangfireMetrics.WorkflowCount.Add(1, workflowTags);
    }
}

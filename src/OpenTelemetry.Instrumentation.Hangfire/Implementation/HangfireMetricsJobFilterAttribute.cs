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
        var executionTags = HangfireTagBuilder.BuildExecutionTags(performedContext.BackgroundJob.Job, performedContext.Exception);

        HangfireMetrics.ExecutionCount.Add(1, executionTags);

        if (performedContext.Items.TryGetValue(StopwatchKey, out var stopwatchObj) && stopwatchObj is Stopwatch stopwatch)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;

            HangfireMetrics.ExecutionDuration.Record(duration, executionTags);
        }
    }
}

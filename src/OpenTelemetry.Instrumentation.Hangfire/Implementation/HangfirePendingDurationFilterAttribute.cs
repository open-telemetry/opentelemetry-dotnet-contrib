// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using OpenTelemetry.Metrics;
using DateTime = System.DateTime;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire filter that records workflow.execution.duration metric for the pending state.
/// </summary>
/// <remarks>
/// This filter captures the EnqueuedAt timestamp when a job enters the Enqueued state
/// and records workflow.execution.duration{state="pending"} when the job starts executing.
/// This represents the time a job spent waiting in the queue before execution started.
/// </remarks>
internal sealed class HangfirePendingDurationFilterAttribute : JobFilterAttribute, IServerFilter, IElectStateFilter
{
    private const string EnqueuedAtParameter = "OpenTelemetry.EnqueuedAt";
    private readonly HangfireMetricsInstrumentationOptions options;

#pragma warning disable CA1019 // Define accessors for attribute arguments
    public HangfirePendingDurationFilterAttribute(HangfireMetricsInstrumentationOptions options)
#pragma warning restore CA1019 // Define accessors for attribute arguments
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnStateElection(ElectStateContext context)
    {
        // When a job transitions to Enqueued state, capture the EnqueuedAt timestamp
        if (context.CandidateState is EnqueuedState enqueuedState)
        {
            try
            {
                var enqueuedAt = enqueuedState.EnqueuedAt;
                context.Connection.SetJobParameter(
                    context.BackgroundJob.Id,
                    EnqueuedAtParameter,
                    JobHelper.SerializeDateTime(enqueuedAt));
            }
            catch
            {
                // Skip storing timestamp if parameter write fails
                // Instrumentation must never break Hangfire's scheduling pipeline
            }
        }
    }

    public void OnPerforming(PerformingContext performingContext)
    {
        try
        {
            // Retrieve the EnqueuedAt timestamp that was stored when the job was enqueued
            var enqueuedAtStr = performingContext.Connection.GetJobParameter(
                performingContext.BackgroundJob.Id,
                EnqueuedAtParameter);

            if (!string.IsNullOrEmpty(enqueuedAtStr))
            {
                var enqueuedAt = JobHelper.DeserializeDateTime(enqueuedAtStr);
                var pendingDuration = (DateTime.UtcNow - enqueuedAt).TotalSeconds;

                // Record workflow.execution.duration with state="pending"
                var tags = HangfireTagBuilder.BuildExecutionTags(
                    performingContext.BackgroundJob,
                    this.options.DisplayNameFunc,
                    exception: null,
                    workflowState: HangfireTagBuilder.StatePending);
                HangfireMetrics.ExecutionDuration.Record(pendingDuration, tags);
            }
        }
        catch
        {
            // Skip recording if parameter retrieval fails
        }
    }

    public void OnPerformed(PerformedContext performedContext)
    {
        // No-op: This filter only handles pending state duration in OnPerforming
    }
}

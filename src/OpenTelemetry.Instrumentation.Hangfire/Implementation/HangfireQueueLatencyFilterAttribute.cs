// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using DateTime = System.DateTime;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire filter that records queue latency metrics.
/// </summary>
/// <remarks>
/// This filter captures the EnqueuedAt timestamp when a job enters the Enqueued state
/// and calculates queue latency when the job starts executing.
/// </remarks>
internal sealed class HangfireQueueLatencyFilterAttribute : JobFilterAttribute, IServerFilter, IElectStateFilter
{
    private const string EnqueuedAtParameter = "OpenTelemetry.EnqueuedAt";

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
                var queueLatency = (DateTime.UtcNow - enqueuedAt).TotalSeconds;

                var tags = HangfireTagBuilder.BuildCommonTags(performingContext.BackgroundJob.Job);
                HangfireMetrics.QueueLatency.Record(queueLatency, tags);
            }
        }
        catch
        {
            // Skip recording if parameter retrieval fails
        }
    }

    public void OnPerformed(PerformedContext performedContext)
    {
        // No-op: This filter only handles queue latency in OnPerforming
    }
}

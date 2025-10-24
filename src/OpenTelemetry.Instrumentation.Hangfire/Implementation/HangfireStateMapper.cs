// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.States;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Maps Hangfire-specific state names to OpenTelemetry workflow semantic convention state values.
/// </summary>
internal static class HangfireStateMapper
{
    /// <summary>
    /// Maps Hangfire state names to workflow execution semantic convention state values.
    /// Note: ScheduledState is NOT mapped here as it represents a workflow-level state,
    /// not an execution-level state. Use MapWorkflowState() for scheduled jobs.
    /// </summary>
    /// <param name="hangfireState">Hangfire state name.</param>
    /// <returns>Mapped workflow execution state value, or <see langword="null"/> if the state is not recognized.</returns>
    public static string? MapExecutionState(string? hangfireState)
    {
        if (hangfireState == EnqueuedState.StateName ||
            hangfireState == AwaitingState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Pending;
        }

        if (hangfireState == ProcessingState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Executing;
        }

        if (hangfireState == SucceededState.StateName ||
            hangfireState == DeletedState.StateName ||
            hangfireState == FailedState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Completed;
        }

        return null;
    }

    /// <summary>
    /// Maps Hangfire state names to workflow-level semantic convention state values.
    /// Tracks the complete workflow lifecycle: scheduled jobs waiting for trigger time,
    /// enqueued jobs waiting in queue, executing jobs, and completed jobs.
    /// Note: "pending" at workflow level includes both scheduled (waiting for time) and enqueued (waiting for worker).
    /// </summary>
    /// <param name="hangfireState">Hangfire state name.</param>
    /// <returns>Mapped workflow state value, or <see langword="null"/> if the state is not recognized.</returns>
    public static string? MapWorkflowState(string? hangfireState)
    {
        // Scheduled and Enqueued/Awaiting are both "pending" at workflow level
        // Scheduled = waiting for scheduled time to arrive (trigger.type=schedule)
        // Enqueued = waiting in queue for worker (trigger.type=api/cron)
        if (hangfireState == ScheduledState.StateName ||
            hangfireState == EnqueuedState.StateName ||
            hangfireState == AwaitingState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Pending;
        }

        if (hangfireState == ProcessingState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Executing;
        }

        if (hangfireState == SucceededState.StateName ||
            hangfireState == DeletedState.StateName ||
            hangfireState == FailedState.StateName)
        {
            return WorkflowAttributes.WorkflowStateValues.Completed;
        }

        return null;
    }
}

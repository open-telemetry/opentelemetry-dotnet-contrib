// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Tag builder for creating standardized OpenTelemetry tag lists for Hangfire job metrics.
/// Follows OpenTelemetry workflow semantic conventions.
/// </summary>
internal static class HangfireTagBuilder
{
    // Tag name constants following OpenTelemetry workflow semantic conventions
    // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/workflow/workflow-metrics.md
    internal const string TagWorkflowTaskName = "workflow.task.name";
    internal const string TagWorkflowExecutionOutcome = "workflow.execution.outcome";
    internal const string TagWorkflowExecutionState = "workflow.execution.state";
    internal const string TagWorkflowPlatformName = "workflow.platform.name";
    internal const string TagWorkflowTriggerType = "workflow.trigger.type";
    internal const string TagErrorType = "error.type";

    // Outcome values per semantic conventions
    internal const string OutcomeSuccess = "success";
    internal const string OutcomeFailure = "failure";

    // State values per semantic conventions
    internal const string StatePending = "pending";
    internal const string StateExecuting = "executing";
    internal const string StateCompleted = "completed";

    // Platform name constant
    internal const string PlatformHangfire = "hangfire";

    // Trigger type constants
    internal const string TriggerTypeCron = "cron";
    internal const string TriggerTypeApi = "api";

    /// <summary>
    /// Creates a tag list with common job metadata following workflow semantic conventions.
    /// Includes required workflow.task.name and recommended workflow.platform.name.
    /// Also includes custom Hangfire-specific attributes (job.type, job.method).
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <returns>Tag list with common job tags.</returns>
    public static TagList BuildCommonTags(Job job)
    {
        var tags = new TagList
        {
            GetTaskName(job),
            GetPlatformName(),
        };
        return tags;
    }

    /// <summary>
    /// Creates a tag list representing workflow execution state transitions.
    /// Includes required workflow.task.name, workflow.execution.state, and workflow.trigger.type tags,
    /// recommended workflow.platform.name, and conditionally required error.type.
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="workflowState">The workflow state value.</param>
    /// <param name="errorType">Optional error type to annotate failure states.</param>
    /// <param name="recurringJobId">Optional recurring job ID if this job was triggered by a recurring job.</param>
    /// <returns>Tag list suitable for workflow.execution.status metric.</returns>
    public static TagList BuildStateTags(Job job, string workflowState, string? errorType, string? recurringJobId)
    {
        var tags = new TagList
        {
            GetTaskName(job),
            GetPlatformName(),
            GetState(workflowState),
            GetTriggerType(recurringJobId),
        };

        if (!string.IsNullOrEmpty(errorType))
        {
            tags.Add(new KeyValuePair<string, object?>(TagErrorType, errorType));
        }

        return tags;
    }

    /// <summary>
    /// Maps Hangfire state names to workflow semantic convention state values.
    /// </summary>
    /// <param name="hangfireState">Hangfire state name.</param>
    /// <returns>Mapped workflow state value, or <see langword="null"/> if the state is not recognized.</returns>
    public static string? MapWorkflowState(string? hangfireState)
    {
        if (hangfireState == ScheduledState.StateName ||
            hangfireState == EnqueuedState.StateName ||
            hangfireState == AwaitingState.StateName)
        {
            return StatePending;
        }

        if (hangfireState == ProcessingState.StateName)
        {
            return StateExecuting;
        }

        if (hangfireState == SucceededState.StateName ||
            hangfireState == DeletedState.StateName ||
            hangfireState == FailedState.StateName)
        {
            return StateCompleted;
        }

        return null;
    }

    /// <summary>
    /// Creates a tag list with execution result tags following workflow semantic conventions.
    /// Includes required attributes (workflow.task.name, workflow.execution.outcome),
    /// recommended attributes (workflow.platform.name),
    /// conditionally required attributes (error.type if failed)
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="exception">The exception, if any occurred.</param>
    /// <returns>Tag list with execution result tags.</returns>
    public static TagList BuildExecutionTags(Job job, Exception? exception)
    {
        var tags = new TagList
        {
            GetTaskName(job),
            GetPlatformName(),
            GetOutcome(exception),
        };

        // Conditionally Required: error.type (if and only if the task run failed)
        if (exception is not null)
        {
            tags.Add(GetErrorType(exception));
        }

        return tags;
    }

    /// <summary>
    /// Creates a tag list for workflow.execution.errors metric following workflow semantic conventions.
    /// Includes required attributes (error.type, workflow.task.name),
    /// recommended attributes (workflow.platform.name).
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>Tag list with error tags.</returns>
    public static TagList BuildErrorTags(Job job, Exception exception)
    {
        var tags = new TagList
        {
            GetErrorType(exception),
            GetTaskName(job),
            GetPlatformName(),
        };

        return tags;
    }

    // Required workflow attributes
    private static KeyValuePair<string, object?> GetTaskName(Job job) =>
        new(TagWorkflowTaskName, job.ToString() ?? "unknown");

    private static KeyValuePair<string, object?> GetOutcome(Exception? exception) =>
        new(TagWorkflowExecutionOutcome, exception is null ? OutcomeSuccess : OutcomeFailure);

    private static KeyValuePair<string, object?> GetTriggerType(string? recurringJobId)
    {
        // Check if job was triggered by a recurring job (cron)
        if (!string.IsNullOrEmpty(recurringJobId))
        {
            return new(TagWorkflowTriggerType, TriggerTypeCron);
        }

        // Default to API trigger (fire-and-forget, scheduled, continuations)
        return new(TagWorkflowTriggerType, TriggerTypeApi);
    }

    // Recommended workflow attributes
    private static KeyValuePair<string, object?> GetPlatformName() =>
        new(TagWorkflowPlatformName, PlatformHangfire);

    private static KeyValuePair<string, object?> GetState(string workflowState) =>
        new(TagWorkflowExecutionState, workflowState);

    // Conditionally required workflow attributes
    private static KeyValuePair<string, object?> GetErrorType(Exception exception)
    {
        var loggedException = exception;

        if (loggedException is JobPerformanceException pe)
        {
            loggedException = pe.InnerException;
        }

        return new KeyValuePair<string, object?>(TagErrorType, loggedException.GetType().FullName ?? loggedException.GetType().Name);
    }
}

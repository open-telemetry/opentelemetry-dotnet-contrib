// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Hangfire;
using Hangfire.Server;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Tag builder for creating standardized OpenTelemetry tag lists for Hangfire job metrics.
/// Follows OpenTelemetry workflow semantic conventions.
/// </summary>
internal static class HangfireTagBuilder
{

    /// <summary>
    /// Creates a tag list with common job metadata following workflow semantic conventions.
    /// Includes required workflow.task.name and recommended workflow.platform.name.
    /// Also includes custom Hangfire-specific attributes (job.type, job.method).
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <returns>Tag list with common job tags.</returns>
    public static TagList BuildCommonTags(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc)
    {
        var tags = new TagList
        {
            GetTaskName(backgroundJob, displayNameFunc),
            GetPlatformName(),
        };
        return tags;
    }

    /// <summary>
    /// Creates a tag list representing workflow execution state transitions.
    /// Includes required workflow.task.name and workflow.execution.state tags,
    /// recommended workflow.platform.name, and conditionally required error.type.
    /// Note: workflow.trigger.type is NOT included as it belongs to workflow-level metrics only.
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="workflowState">The workflow state value.</param>
    /// <param name="errorType">Optional error type to annotate failure states.</param>
    /// <returns>Tag list suitable for workflow.execution.status metric.</returns>
    public static TagList BuildExecutionstatusTags(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc, string workflowState, string? errorType)
    {
        var tags = new TagList
        {
            GetTaskName(backgroundJob, displayNameFunc),
            GetPlatformName(),
            GetState(workflowState),
        };

        if (!string.IsNullOrEmpty(errorType))
        {
            tags.Add(new KeyValuePair<string, object?>(WorkflowAttributes.AttributeErrorType, errorType));
        }

        return tags;
    }


    /// <summary>
    /// Creates a tag list with execution result tags following workflow semantic conventions.
    /// Includes required attributes (workflow.task.name, workflow.execution.outcome, workflow.execution.state),
    /// recommended attributes (workflow.platform.name),
    /// conditionally required attributes (error.type if failed).
    /// Note: workflow.trigger.type is NOT included as it belongs to workflow-level metrics only.
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="exception">The exception, if any occurred.</param>
    /// <param name="workflowState">The workflow state value (typically "executing" for execution duration).</param>
    /// <returns>Tag list with execution result tags.</returns>
    public static TagList BuildExecutionTags(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc, Exception? exception, string workflowState)
    {
        var tags = new TagList
        {
            GetTaskName(backgroundJob, displayNameFunc),
            GetPlatformName(),
            GetExecutionOutcome(exception),
            GetState(workflowState),
        };

        // Conditionally Required: error.type (if and only if the task run failed)
        if (exception is not null)
        {
            tags.Add(GetErrorType(exception));
        }

        return tags;
    }

    /// <summary>
    /// Creates a tag list for workflow.execution.count metric following workflow semantic conventions.
    /// Includes required attributes (workflow.task.name, workflow.execution.outcome),
    /// recommended attributes (workflow.platform.name),
    /// conditionally required attributes (error.type if failed).
    /// Note: Does NOT include workflow.execution.state as it's not specified in semantic conventions for count metric.
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="exception">The exception, if any occurred.</param>
    /// <returns>Tag list suitable for workflow.execution.count metric.</returns>
    public static TagList BuildExecutionCountTags(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc, Exception? exception)
    {
        var tags = new TagList
        {
            GetTaskName(backgroundJob, displayNameFunc),
            GetPlatformName(),
            GetExecutionOutcome(exception),
        };

        // Conditionally Required: error.type (if and only if the task run failed)
        if (exception is not null)
        {
            tags.Add(GetErrorType(exception));
        }

        return tags;
    }

    /// <summary>
    /// Creates a tag list for workflow-level metrics following workflow semantic conventions.
    /// Includes required attributes (workflow.definition.name, workflow.outcome, workflow.trigger.type),
    /// recommended attributes (workflow.platform.name),
    /// conditionally required attributes (error.type if failed).
    /// Used for workflow.count and other workflow-level (not execution-level) metrics.
    /// </summary>
    /// <param name="job">The Hangfire job.</param>
    /// <param name="exception">The exception, if any occurred.</param>
    /// <param name="recurringJobId">Optional recurring job ID if this job was triggered by a recurring job.</param>
    /// <returns>Tag list suitable for workflow.count metric.</returns>
    public static TagList BuildWorkflowTags(BackgroundJob job, Func<BackgroundJob, string> displayNameFunc, Exception? exception, string? recurringJobId)
    {
        var tags = new TagList
        {
            GetDefinitionName(job, displayNameFunc),
            GetPlatformName(),
            GetWorkflowOutcome(exception),
            GetTriggerType(recurringJobId),
        };

        // Conditionally Required: error.type (if and only if the workflow execution failed)
        if (exception is not null)
        {
            tags.Add(GetErrorType(exception));
        }

        return tags;
    }

    /// <summary>
    /// Creates a tag list for workflow.status metric following workflow semantic conventions.
    /// Includes required attributes (workflow.definition.name, workflow.state, workflow.trigger.type),
    /// recommended attributes (workflow.platform.name),
    /// conditionally required attributes (error.type if failed).
    /// Used for tracking workflow state transitions (e.g., scheduled jobs).
    /// </summary>
    /// <param name="backgroundJob">The Hangfire background job.</param>
    /// <param name="displayNameFunc">Function to generate display name.</param>
    /// <param name="workflowState">The workflow state value (e.g., "pending", "executing", "completed").</param>
    /// <param name="recurringJobId">Optional recurring job ID if this job was triggered by a recurring job.</param>
    /// <param name="errorType">Optional error type to annotate failure states.</param>
    /// <returns>Tag list suitable for workflow.status metric.</returns>
    public static TagList BuildWorkflowStatusTags(
        BackgroundJob backgroundJob,
        Func<BackgroundJob, string> displayNameFunc,
        string workflowState,
        string? recurringJobId,
        string? errorType)
    {
        var tags = new TagList
        {
            GetDefinitionName(backgroundJob, displayNameFunc),
            GetPlatformName(),
            new KeyValuePair<string, object?>(WorkflowAttributes.AttributeWorkflowState, workflowState),
            GetTriggerType(recurringJobId, isScheduled: workflowState == WorkflowAttributes.WorkflowStateValues.Pending && string.IsNullOrEmpty(recurringJobId)),
        };

        if (!string.IsNullOrEmpty(errorType))
        {
            tags.Add(new KeyValuePair<string, object?>(WorkflowAttributes.AttributeErrorType, errorType));
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
    public static TagList BuildErrorTags(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc, Exception exception)
    {
        var tags = new TagList
        {
            GetErrorType(exception),
            GetTaskName(backgroundJob, displayNameFunc),
            GetPlatformName(),
        };

        return tags;
    }

    // Required workflow attributes
    private static KeyValuePair<string, object?> GetTaskName(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc) =>
        new(WorkflowAttributes.AttributeWorkflowTaskName, displayNameFunc(backgroundJob));

    private static KeyValuePair<string, object?> GetDefinitionName(BackgroundJob backgroundJob, Func<BackgroundJob, string> displayNameFunc) =>
        new(WorkflowAttributes.AttributeWorkflowDefinitionName, displayNameFunc(backgroundJob));

    private static KeyValuePair<string, object?> GetExecutionOutcome(Exception? exception) =>
        new(WorkflowAttributes.AttributeWorkflowExecutionOutcome, exception is null ? WorkflowAttributes.WorkflowOutcomeValues.Success : WorkflowAttributes.WorkflowOutcomeValues.Failure);

    private static KeyValuePair<string, object?> GetWorkflowOutcome(Exception? exception) =>
        new(WorkflowAttributes.AttributeWorkflowOutcome, exception is null ? WorkflowAttributes.WorkflowOutcomeValues.Success : WorkflowAttributes.WorkflowOutcomeValues.Failure);

    private static KeyValuePair<string, object?> GetTriggerType(string? recurringJobId, bool isScheduled = false)
    {
        // Check if job was triggered by a recurring job (cron)
        if (!string.IsNullOrEmpty(recurringJobId))
        {
            return new(WorkflowAttributes.AttributeWorkflowTriggerType, WorkflowAttributes.WorkflowTriggerTypeValues.Cron);
        }

        // Check if job is scheduled for future execution
        if (isScheduled)
        {
            return new(WorkflowAttributes.AttributeWorkflowTriggerType, WorkflowAttributes.WorkflowTriggerTypeValues.Schedule);
        }

        // Default to API trigger (fire-and-forget, continuations)
        return new(WorkflowAttributes.AttributeWorkflowTriggerType, WorkflowAttributes.WorkflowTriggerTypeValues.Api);
    }

    // Recommended workflow attributes
    private static KeyValuePair<string, object?> GetPlatformName() =>
        new(WorkflowAttributes.AttributeWorkflowPlatformName, WorkflowAttributes.WorkflowPlatformNameValues.Hangfire);

    private static KeyValuePair<string, object?> GetState(string workflowState) =>
        new(WorkflowAttributes.AttributeWorkflowExecutionState, workflowState);

    // Conditionally required workflow attributes
    private static KeyValuePair<string, object?> GetErrorType(Exception exception)
    {
        var loggedException = exception;

        if (loggedException is JobPerformanceException { InnerException: not null } pe)
        {
            loggedException = pe.InnerException!;
        }

        return new KeyValuePair<string, object?>(WorkflowAttributes.AttributeErrorType, loggedException.GetType().FullName ?? loggedException.GetType().Name);
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire state change filter responsible for emitting workflow execution status metrics.
/// </summary>
internal sealed class HangfireMetricsStateFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly HangfireMetricsInstrumentationOptions options;

#pragma warning disable CA1019 // Define accessors for attribute arguments
    public HangfireMetricsStateFilter(HangfireMetricsInstrumentationOptions options)
#pragma warning restore CA1019 // Define accessors for attribute arguments
    {
        this.options = options;
    }

    private enum StateTransitionDirection
    {
        Applied,
        Unapplied,
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        this.RecordWorkflowStateChange(context, StateTransitionDirection.Applied);
        this.RecordExecutionStateChange(context, StateTransitionDirection.Applied);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        this.RecordWorkflowStateChange(context, StateTransitionDirection.Unapplied);
        this.RecordExecutionStateChange(context, StateTransitionDirection.Unapplied);
    }

    private static string? GetErrorTypeFromNewState(IState state)
    {
        if (!string.Equals(state.Name, FailedState.StateName, StringComparison.Ordinal))
        {
            return null;
        }

        if (state is FailedState { Exception: not null } failedState)
        {
            var exceptionType = failedState.Exception.GetType();
            return exceptionType.FullName ?? exceptionType.Name;
        }

        return TryGetExceptionTypeFromSerializedData(state.SerializeData());
    }

    private static string? GetErrorTypeFromOldState(ApplyStateContext context)
    {
        if (!string.Equals(context.OldStateName, FailedState.StateName, StringComparison.Ordinal))
        {
            return null;
        }

        var stateData = context.Connection.GetStateData(context.BackgroundJob.Id);
        return stateData != null ? TryGetExceptionTypeFromSerializedData(stateData.Data) : null;
    }

    private static string? TryGetExceptionTypeFromSerializedData(IDictionary<string, string>? data)
    {
        if (data == null)
        {
            return null;
        }

        if (data.TryGetValue("ExceptionType", out var exceptionType) && !string.IsNullOrWhiteSpace(exceptionType))
        {
            return exceptionType;
        }

        return null;
    }

    private static string? TryGetRecurringJobId(ApplyStateContext context)
    {
        try
        {
            return context.Connection.GetJobParameter(
                context.BackgroundJob.Id,
                "RecurringJobId");
        }
        catch
        {
            return null;
        }
    }

    private void RecordWorkflowStateChange(
        ApplyStateContext context,
        StateTransitionDirection direction)
    {
        var oldState = HangfireStateMapper.MapWorkflowState(this.MapCustomState(context.OldStateName));
        var newState = HangfireStateMapper.MapWorkflowState(this.MapCustomState(context.NewState.Name));

        // Determine which state to record based on transition direction
        var stateToRecord = direction == StateTransitionDirection.Applied ? newState : oldState;

        // Only record if semantic state actually changed
        if (stateToRecord == null || oldState == newState)
        {
            return;
        }

        var recurringJobId = TryGetRecurringJobId(context);
        var errorType = direction == StateTransitionDirection.Applied
            ? GetErrorTypeFromNewState(context.NewState)
            : GetErrorTypeFromOldState(context);

        var tags = HangfireTagBuilder.BuildWorkflowStatusTags(
            context.BackgroundJob,
            stateToRecord,
            recurringJobId,
            errorType);

        var delta = direction == StateTransitionDirection.Applied ? 1 : -1;
        HangfireMetrics.WorkflowStatus.Add(delta, HangfireTagBuilder.Enrich(tags, context.BackgroundJob, this.options));
    }

    private void RecordExecutionStateChange(
        ApplyStateContext context,
        StateTransitionDirection direction)
    {
        var oldState = HangfireStateMapper.MapExecutionState(this.MapCustomState(context.OldStateName));
        var newState = HangfireStateMapper.MapExecutionState(this.MapCustomState(context.NewState.Name));

        // Determine which state to record based on transition direction
        var stateToRecord = direction == StateTransitionDirection.Applied ? newState : oldState;

        // Only record if semantic state actually changed
        if (stateToRecord == null || oldState == newState)
        {
            return;
        }

        var errorType = direction == StateTransitionDirection.Applied
            ? GetErrorTypeFromNewState(context.NewState)
            : GetErrorTypeFromOldState(context);

        var tags = HangfireTagBuilder.BuildExecutionstatusTags(
            context.BackgroundJob,
            stateToRecord,
            errorType);

        var delta = direction == StateTransitionDirection.Applied ? 1 : -1;
        HangfireMetrics.ExecutionStatus.Add(delta, HangfireTagBuilder.Enrich(tags, context.BackgroundJob, this.options));
    }

    private string? MapCustomState(string? stateName) =>
        stateName != null && this.options.CustomStateMappings?.TryGetValue(stateName, out var builtInState) == true
            ? builtInState
            : stateName;
}

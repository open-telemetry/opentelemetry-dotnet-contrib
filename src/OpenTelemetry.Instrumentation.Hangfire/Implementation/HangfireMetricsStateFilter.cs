// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire state change filter responsible for emitting workflow execution status metrics.
/// </summary>
internal sealed class HangfireMetricsStateFilter : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var workflowState = HangfireTagBuilder.MapWorkflowState(context.NewState.Name);
        if (workflowState == null)
        {
            return;
        }

        var errorType = GetErrorTypeFromNewState(context.NewState);
        var recurringJobId = GetRecurringJobId(context);
        var tags = HangfireTagBuilder.BuildStateTags(
            context.BackgroundJob.Job,
            workflowState,
            errorType,
            recurringJobId);

        HangfireMetrics.ExecutionStatus.Add(1, tags);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var workflowState = HangfireTagBuilder.MapWorkflowState(context.OldStateName);
        if (workflowState == null)
        {
            return;
        }

        var errorType = GetErrorTypeFromOldState(context);
        var recurringJobId = GetRecurringJobId(context);
        var tags = HangfireTagBuilder.BuildStateTags(
            context.BackgroundJob.Job,
            workflowState,
            errorType,
            recurringJobId);

        HangfireMetrics.ExecutionStatus.Add(-1, tags);
    }

    private static string? GetErrorTypeFromNewState(IState state)
    {
        if (!string.Equals(state.Name, FailedState.StateName, StringComparison.Ordinal))
        {
            return null;
        }

        if (state is FailedState failedState && failedState.Exception != null)
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

        StateData? stateData = context.Connection.GetStateData(context.BackgroundJob.Id);
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

    private static string? GetRecurringJobId(ApplyStateContext context)
    {
        try
        {
            return context.Connection.GetJobParameter(context.BackgroundJob.Id, "RecurringJobId");
        }
        catch
        {
            // Parameter doesn't exist or couldn't be retrieved
            return null;
        }
    }
}

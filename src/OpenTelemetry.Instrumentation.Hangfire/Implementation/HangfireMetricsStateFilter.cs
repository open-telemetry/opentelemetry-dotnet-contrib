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
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var workflowState = HangfireTagBuilder.MapWorkflowState(context.NewState.Name);
        if (workflowState == null)
        {
            return;
        }

        var errorType = GetErrorTypeFromNewState(context.NewState);
        var tags = HangfireTagBuilder.BuildStateTags(
            context.BackgroundJob,
            this.options.DisplayNameFunc,
            workflowState,
            errorType);

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
        var tags = HangfireTagBuilder.BuildStateTags(
            context.BackgroundJob,
            this.options.DisplayNameFunc,
            workflowState,
            errorType);

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
}

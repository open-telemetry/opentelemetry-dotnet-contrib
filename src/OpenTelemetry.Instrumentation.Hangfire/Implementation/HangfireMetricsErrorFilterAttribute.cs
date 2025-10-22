// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire.Common;
using Hangfire.Server;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire filter that records OpenTelemetry error metrics for job execution failures.
/// Follows OpenTelemetry workflow semantic conventions for workflow.execution.errors metric.
/// </summary>
internal sealed class HangfireMetricsErrorFilterAttribute : JobFilterAttribute, IServerFilter
{
    private readonly HangfireMetricsInstrumentationOptions options;

#pragma warning disable CA1019 // Define accessors for attribute arguments
    public HangfireMetricsErrorFilterAttribute(HangfireMetricsInstrumentationOptions options)
#pragma warning restore CA1019 // Define accessors for attribute arguments
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnPerforming(PerformingContext performingContext)
    {
    }

    public void OnPerformed(PerformedContext performedContext)
    {
        if (performedContext.Exception != null)
        {
            var errorTags = HangfireTagBuilder.BuildErrorTags(
                performedContext.BackgroundJob,
                this.options.DisplayNameFunc,
                performedContext.Exception);

            HangfireMetrics.ExecutionErrors.Add(1, errorTags);
        }
    }
}

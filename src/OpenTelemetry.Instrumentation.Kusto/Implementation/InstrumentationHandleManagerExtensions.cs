// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Provides extension methods for <see cref="InstrumentationHandleManager" />.
/// </summary>
internal static class InstrumentationHandleManagerExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> if tracing is active (i.e., there is at least one tracing handle); otherwise, <see langword="false"/>.
    /// </summary>
    /// <param name="handleManager">
    /// The <see cref="InstrumentationHandleManager"/> to check for active tracing handles.
    /// </param>
    /// <returns><see langword="true"/> if tracing is active; otherwise, <see langword="false"/>.</returns>
    public static bool IsTracingActive(this InstrumentationHandleManager handleManager)
    {
        return handleManager.TracingHandles > 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> if metrics is active (i.e., there is at least one metrics handle); otherwise, <see langword="false"/>.
    /// </summary>
    /// <param name="handleManager">
    /// The <see cref="InstrumentationHandleManager"/> to check for active metrics handles.
    /// </param>
    /// <returns><see langword="true"/> if metrics is active; otherwise, <see langword="false"/>.</returns>
    public static bool IsMetricsActive(this InstrumentationHandleManager handleManager)
    {
        return handleManager.MetricHandles > 0;
    }
}

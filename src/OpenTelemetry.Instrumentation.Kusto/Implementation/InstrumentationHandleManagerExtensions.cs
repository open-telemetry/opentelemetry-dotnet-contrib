// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class InstrumentationHandleManagerExtensions
{
    public static bool IsTracingActive(this InstrumentationHandleManager handleManager)
    {
        return handleManager.TracingHandles > 0;
    }

    public static bool IsMetricsActive(this InstrumentationHandleManager handleManager)
    {
        return handleManager.MetricHandles > 0;
    }
}

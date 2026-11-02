// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

/// <summary>
/// Hangfire metrics instrumentation following OpenTelemetry workflow semantic conventions.
/// </summary>
internal sealed class HangfireMetricsInstrumentation : IDisposable
{
    private readonly List<object> filters = [];

    public HangfireMetricsInstrumentation(HangfireMetricsInstrumentationOptions options)
    {
        this.AddFilter(new HangfireMetricsStateFilter(options));
        this.AddFilter(new HangfireMetricsErrorFilterAttribute(options));

        // Only register pending duration filter if enabled (requires DB call per job)
        if (options.RecordQueueLatency)
        {
            this.AddFilter(new HangfirePendingDurationFilterAttribute(options));
        }

        // Added last so that its stopwatch excludes the work of the other metrics filters
        this.AddFilter(new HangfireMetricsJobFilterAttribute(options));
    }

    public void Dispose()
    {
        foreach (var filter in this.filters)
        {
            GlobalJobFilters.Filters.Remove(filter);
        }
    }

    private void AddFilter(object filter)
    {
        this.filters.Add(filter);
        GlobalJobFilters.Filters.Add(filter);
    }
}

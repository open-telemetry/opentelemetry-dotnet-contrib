// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Hangfire;
using Hangfire.States;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Options for Hangfire metrics instrumentation.
/// </summary>
public sealed class HangfireMetricsInstrumentationOptions
{
    private static readonly HashSet<string> BuiltInStates = new(StringComparer.Ordinal)
    {
        AwaitingState.StateName,
        DeletedState.StateName,
        EnqueuedState.StateName,
        FailedState.StateName,
        ProcessingState.StateName,
        ScheduledState.StateName,
        SucceededState.StateName,
    };

    private IReadOnlyDictionary<string, string>? customStateMappings;

    /// <summary>
    /// Gets or sets a value indicating whether to record the pending state duration in metrics.
    /// </summary>
    /// <remarks>
    /// When enabled, records workflow.execution.duration with state="pending", representing
    /// the time jobs spend waiting in the queue before execution starts.
    /// This requires an additional database call per job execution to retrieve the enqueue timestamp.
    /// Default is <see langword="false"/> to avoid performance impact in high-throughput scenarios.
    /// When disabled, only execution duration (state="executing") is recorded.
    /// </remarks>
    public bool RecordQueueLatency { get; set; }

    /// <summary>
    /// Gets or sets a delegate to enrich a recorded metric with additional custom tags.
    /// </summary>
    public Func<BackgroundJob, TagList>? EnrichWithBackgroundJob { get; set; }

    /// <summary>
    /// Gets or sets mappings from custom Hangfire state names to built-in Hangfire state names.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomStateMappings
    {
        get => this.customStateMappings;
        set
        {
            if (value == null)
            {
                this.customStateMappings = null;
                return;
            }

            var copy = new Dictionary<string, string>(value.Count, StringComparer.Ordinal);

            foreach (var entry in value)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || BuiltInStates.Contains(entry.Key))
                {
                    throw new ArgumentException($"The custom state name '{entry.Key}' was null, empty, white-space or a built-in Hangfire state name.", nameof(value));
                }

                if (!BuiltInStates.Contains(entry.Value))
                {
                    throw new ArgumentException($"The state name '{entry.Value}' mapped from custom state '{entry.Key}' is not a built-in Hangfire state name.", nameof(value));
                }

                copy[entry.Key] = entry.Value;
            }

            this.customStateMappings = copy;
        }
    }
}

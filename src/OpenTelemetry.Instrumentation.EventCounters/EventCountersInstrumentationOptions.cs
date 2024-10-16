// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// EventCounters Instrumentation Options.
/// </summary>
public class EventCountersInstrumentationOptions
{
    internal readonly HashSet<string> EventSourceNames = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountersInstrumentationOptions"/> class.
    /// </summary>
    public EventCountersInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal EventCountersInstrumentationOptions(IConfiguration configuration)
    {
        Debug.Assert(configuration != null, "configuration was null");

        if (configuration!.TryGetIntValue(
                EventCountersInstrumentationEventSource.Log,
                "OTEL_DOTNET_EVENTCOUNTERS_REFRESH_INTERVAL_SECS",
                out var refreshIntervalSecs))
        {
            this.RefreshIntervalSecs = refreshIntervalSecs;
        }

        if (configuration!.TryGetValue<string[]>(
                EventCountersInstrumentationEventSource.Log,
                "OTEL_DOTNET_EVENTCOUNTERS_SOURCES",
                this.TrySplitString,
                out var eventSourceNames) && eventSourceNames != null)
        {
            this.AddEventSources(eventSourceNames);
        }
    }

    /// <summary>
    /// Gets or sets the subscription interval in seconds for reading values
    /// from the configured EventCounters.
    /// </summary>
    public int RefreshIntervalSecs { get; set; } = 1;

    /// <summary>
    /// Listens to EventCounters from the given EventSource name.
    /// </summary>
    /// <param name="names">The EventSource names to listen to.</param>
    public void AddEventSources(params string[] names)
    {
        if (names.Contains("System.Runtime"))
        {
            throw new NotSupportedException("Use the `OpenTelemetry.Instrumentation.Runtime` or `OpenTelemetry.Instrumentation.Process` instrumentations.");
        }

        this.EventSourceNames.UnionWith(names);
    }

    /// <summary>
    /// Returns whether or not an EventSource should be enabled on the EventListener.
    /// </summary>
    /// <param name="eventSourceName">The EventSource name.</param>
    /// <returns><c>true</c> when an EventSource with the name <paramref name="eventSourceName"/> should be enabled.</returns>
    internal bool ShouldListenToSource(string eventSourceName)
    {
        return this.EventSourceNames.Contains(eventSourceName);
    }

    /// <summary>
    /// Tries to split the provided string using a comma as the separator.
    /// </summary>
    /// <param name="value">The string to split.</param>
    /// <param name="parsedValue">The array of strings after the split.</param>
    /// <returns><c>true</c> if the split was successful; otherwise, <c>false</c>.</returns>
    private bool TrySplitString(string value, out string[]? parsedValue)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parsedValue = value.Split(',');
            return true;
        }

        parsedValue = null;
        return false;
    }
}

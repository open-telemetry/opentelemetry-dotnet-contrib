// <copyright file="EventCountersMetrics.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// .NET EventCounters Instrumentation.
/// </summary>
internal class EventCountersMetrics : EventListener
{
    private static readonly AssemblyName AssemblyName = typeof(EventCountersMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());
    private static readonly Regex InstrumentNameRegex = new(
        @"^[a-zA-Z][-.\w]{0,62}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly EventCountersInstrumentationOptions options;
    private readonly ConcurrentQueue<EventSource> preInitEventSources = new();
    private readonly ConcurrentDictionary<(string, string), Instrument> instruments = new();
    private readonly ConcurrentDictionary<(string, string), double> values = new();
    private readonly HashSet<string> instrumentNames = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountersMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public EventCountersMetrics(EventCountersInstrumentationOptions options)
    {
        this.options = options;

        while (this.preInitEventSources.TryDequeue(out EventSource source))
        {
            if (this.options.ShouldListenToSource(source.Name))
            {
                this.EnableEvents(source, EventLevel.LogAlways, EventKeywords.None, GetEnableEventsArguments(this.options));
            }
        }
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (this.options == null)
        {
            this.preInitEventSources.Enqueue(source);
        }
        else if (this.options.ShouldListenToSource(source.Name))
        {
            this.EnableEvents(source, EventLevel.LogAlways, EventKeywords.None, GetEnableEventsArguments(this.options));
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (this.options == null)
        {
            return;
        }

        var eventSourceName = eventData.EventSource.Name;

        if (eventData.EventName != "EventCounters")
        {
            EventCountersInstrumentationEventSource.Log.IgnoreNonEventCountersName(eventSourceName);
            return;
        }

        if (eventData.Payload.Count == 0 || eventData.Payload[0] is not IDictionary<string, object>)
        {
            EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenEventArgsPayloadNotParseable(eventSourceName);
            return;
        }

        var payload = eventData.Payload[0] as IDictionary<string, object>;
        var hasName = payload.TryGetValue("Name", out var nameObj);

        if (!hasName)
        {
            EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenEventArgsWithoutName(eventSourceName);
            return;
        }

        var name = nameObj.ToString();

        var hasMean = payload.TryGetValue("Mean", out var mean);
        var hasIncrement = payload.TryGetValue("Increment", out var increment);

        if (!(hasIncrement ^ hasMean))
        {
            EventCountersInstrumentationEventSource.Log.IgnoreMeanIncrementConflict(eventSourceName);
            return;
        }

        var value = Convert.ToDouble(hasMean ? mean : increment);
        this.EventWritten(hasMean, eventSourceName, name, value);
    }

    private static Dictionary<string, string> GetEnableEventsArguments(EventCountersInstrumentationOptions options) =>
        new() { { "EventCounterIntervalSec", options.RefreshIntervalSecs.ToString() } };

    private static bool IsValidInstrumentName(string name) =>
        !string.IsNullOrWhiteSpace(name) && InstrumentNameRegex.IsMatch(name);

    private void EventWritten(bool isGauge, string eventSourceName, string name, double value)
    {
        try
        {
            ValueTuple<string, string> metricKey = new(eventSourceName, name);
            _ = this.values.AddOrUpdate(metricKey, value, isGauge ? (_, _) => value : (_, existing) => existing + value);

            if (!this.instruments.ContainsKey(metricKey))
            {
                var instrumentName = $"EventCounters.{eventSourceName}.{name}";

                if (!IsValidInstrumentName(instrumentName))
                {
                    EventCountersInstrumentationEventSource.Log.InvalidInstrumentNameWarning(instrumentName);
                }

                Instrument instrument = isGauge
                    ? MeterInstance.CreateObservableGauge(instrumentName, () => this.values[metricKey])
                    : MeterInstance.CreateObservableCounter(instrumentName, () => this.values[metricKey]);
                _ = this.instruments.TryAdd(metricKey, instrument);

                if (!this.instrumentNames.Add(instrumentName))
                {
                    EventCountersInstrumentationEventSource.Log.DuplicateInstrumentNameWarning(instrumentName);
                }
            }
        }
        catch (Exception ex)
        {
            EventCountersInstrumentationEventSource.Log.ErrorWhileWritingEvent(eventSourceName, ex.Message);
        }
    }
}

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
using System.Text;

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// .NET EventCounters Instrumentation.
/// </summary>
internal sealed class EventCountersMetrics : EventListener
{
    internal static readonly Meter MeterInstance = new(typeof(EventCountersMetrics).Assembly.GetName().Name, typeof(EventCountersMetrics).Assembly.GetName().Version.ToString());

    private const string Prefix = "ec";
    private const int MaxInstrumentNameLength = 63;

    private readonly EventCountersInstrumentationOptions options;
    private readonly List<EventSource> preInitEventSources = new();
    private readonly ConcurrentDictionary<(string, string), Instrument> instruments = new();
    private readonly ConcurrentDictionary<(string, string), double> values = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountersMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public EventCountersMetrics(EventCountersInstrumentationOptions options)
    {
        lock (this.preInitEventSources)
        {
            this.options = options;

            foreach (EventSource eventSource in this.preInitEventSources)
            {
                if (this.options.ShouldListenToSource(eventSource.Name))
                {
                    this.EnableEvents(eventSource);
                }
            }

            this.preInitEventSources.Clear();
        }
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        lock (this.preInitEventSources)
        {
            if (this.options == null)
            {
                this.preInitEventSources.Add(eventSource);
            }
            else if (this.options.ShouldListenToSource(eventSource.Name))
            {
                this.EnableEvents(eventSource);
            }
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

        if (eventData.Payload == null || eventData.Payload.Count == 0 || eventData.Payload[0] is not IDictionary<string, object> payload)
        {
            EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenEventArgsPayloadNotParsable(eventSourceName);
            return;
        }

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
        this.UpdateInstrumentWithEvent(hasMean, eventSourceName, name, value);
    }

    private static Dictionary<string, string> GetEnableEventsArguments(EventCountersInstrumentationOptions options) =>
        new() { { "EventCounterIntervalSec", options.RefreshIntervalSecs.ToString() } };

    /// <summary>
    /// If instrument name is too long, abbreviates event source name.
    /// E.g. instrument for `Microsoft-AspNetCore-Server-Kestrel`, `tls-handshakes-per-second`
    /// would be too long (64 chars), so it's abbreviated to `ec.m.a.s.k.tls-handshakes-per-second`
    /// instead of `ec.Microsoft-AspNetCore-Server-Kestrel.tls-handshakes-per-second`.
    ///
    /// If after that instrument name is still invalid, it will be validated and ignored later in the pipeline.
    /// </summary>
    private static string GetInstrumentName(string sourceName, string eventName)
    {
        int totalLength = Prefix.Length + 1 + sourceName.Length + 1 + eventName.Length;
        if (totalLength <= MaxInstrumentNameLength)
        {
            return string.Concat(Prefix, ".", sourceName, ".", eventName);
        }

        var maxEventSourceLength = MaxInstrumentNameLength - Prefix.Length - 1 - eventName.Length;
        if (maxEventSourceLength < 2) 
        {
            // event name is too long, there is not enough space for sourceName.
            // let ec.<eventName> flow to metrics SDK and filter if needed.
            return string.Concat(Prefix, ".", eventName);
        }

        var abbreviation = new StringBuilder(maxEventSourceLength);
        int ind = 0;
        while (ind >= 0 && ind < sourceName.Length)
        {
            while (ind < sourceName.Length && (sourceName[ind] == '.' || sourceName[ind] == '-'))
            {
                ind++;
            }

            if (ind < sourceName.Length)
            {
                if (abbreviation.Length + 2 >= maxEventSourceLength)
                {
                    break;
                }

                abbreviation.Append(char.ToLowerInvariant(sourceName[ind])).Append('.');
            }

            int nextDot = sourceName.IndexOf('.', ind);
            int nextDash = sourceName.IndexOf('-', ind);

            if (nextDot < 0)
            {
                if (nextDash < 0)
                {
                    break;
                }

                ind = nextDash;
            }
            else if (nextDash < 0)
            {
                ind = nextDot;
            }
            else
            {
                ind = Math.Min(nextDot, nextDash);
            }
        }

        return string.Concat(Prefix, ".", abbreviation.ToString(), eventName);
    }

    private void EnableEvents(EventSource eventSource)
    {
        this.EnableEvents(eventSource, EventLevel.Critical, EventKeywords.None, GetEnableEventsArguments(this.options));
    }

    private void UpdateInstrumentWithEvent(bool isGauge, string eventSourceName, string name, double value)
    {
        try
        {
            ValueTuple<string, string> metricKey = new(eventSourceName, name);
            _ = this.values.AddOrUpdate(metricKey, value, isGauge ? (_, _) => value : (_, existing) => existing + value);

            if (!this.instruments.ContainsKey(metricKey))
            {
                var instrumentName = GetInstrumentName(eventSourceName, name);
                Instrument instrument = isGauge
                    ? MeterInstance.CreateObservableGauge(instrumentName, () => this.values[metricKey])
                    : MeterInstance.CreateObservableCounter(instrumentName, () => this.values[metricKey]);
                _ = this.instruments.TryAdd(metricKey, instrument);
            }
        }
        catch (Exception ex)
        {
            EventCountersInstrumentationEventSource.Log.ErrorWhileWritingEvent(eventSourceName, ex.Message);
        }
    }
}

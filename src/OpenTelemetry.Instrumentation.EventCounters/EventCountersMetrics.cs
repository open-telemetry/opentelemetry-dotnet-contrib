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
using System.Globalization;
using System.Linq;

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// .NET EventCounters Instrumentation.
/// </summary>
internal sealed class EventCountersMetrics : EventListener
{
    internal static readonly Meter MeterInstance = new(typeof(EventCountersMetrics).Assembly.GetName().Name, typeof(EventCountersMetrics).Assembly.GetName().Version.ToString());

    private const string Prefix = "ec";
    private const int MaxInstrumentNameLength = 63;

    private static readonly Dictionary<string, string> EnableEventsArguments = new() { { "EventCounterIntervalSec", "1" } };

    private readonly EventCountersInstrumentationOptions options;
    private readonly ConcurrentDictionary<(string, string), Instrument> instruments = new();
    private readonly ConcurrentDictionary<(string, string), double> values = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountersMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public EventCountersMetrics(EventCountersInstrumentationOptions options)
    {
        this.options = options;
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.None, EnableEventsArguments);
        base.OnEventSourceCreated(eventSource);
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (this.options == null)
        {
            return;
        }

        var eventSourceName = eventData.EventSource.Name;

        if (!this.options.ShouldListenToSource(eventSourceName))
        {
            this.DisableEvents(eventData.EventSource);
            return;
        }

        if (eventData.EventName != "EventCounters")
        {
            EventCountersInstrumentationEventSource.Log.IgnoreNonEventCountersName(eventSourceName);
            return;
        }

        if (eventData?.Payload.First() is not IDictionary<string, object> payload)
        {
            EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenEventArgsPayloadNotParsable(eventSourceName);
            return;
        }

        if (!payload.TryGetValue("Name", out var nameObj))
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

        var value = Convert.ToDouble(hasMean ? mean : increment, CultureInfo.InvariantCulture);
        this.UpdateInstrumentWithEvent(hasMean, eventSourceName, name, value);
    }

    /// <summary>
    /// If the resulting instrument name is too long, it trims the event source name
    /// to fit in as many characters as possible keeping the event name intact.
    /// E.g. instrument for `Microsoft-AspNetCore-Server-Kestrel`, `tls-handshakes-per-second`
    /// would be too long (64 chars), so it's shortened to `ec.Microsoft-AspNetCore-Server-Kestre.tls-handshakes-per-second`.
    ///
    /// If there is no room for event source name, returns `ec.{event name}` and
    /// if it's still too long, it will be validated and ignored later in the pipeline.
    /// </summary>
    private static string GetInstrumentName(string sourceName, string eventName)
    {
        int totalLength = Prefix.Length + 1 + sourceName.Length + 1 + eventName.Length;
        if (totalLength <= MaxInstrumentNameLength)
        {
            return string.Concat(Prefix, ".", sourceName, ".", eventName);
        }

        var maxEventSourceLength = MaxInstrumentNameLength - Prefix.Length - 2 - eventName.Length;
        if (maxEventSourceLength < 1)
        {
            // event name is too long, there is not enough space for sourceName.
            // let ec.<eventName> flow to metrics SDK and it will suppress it if needed.
            return string.Concat(Prefix, ".", eventName);
        }

        while (maxEventSourceLength > 0 && (sourceName[maxEventSourceLength - 1] == '.' || sourceName[maxEventSourceLength - 1] == '-'))
        {
            maxEventSourceLength--;
        }

        return string.Concat(Prefix, ".", sourceName.Substring(0, maxEventSourceLength), ".", eventName);
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

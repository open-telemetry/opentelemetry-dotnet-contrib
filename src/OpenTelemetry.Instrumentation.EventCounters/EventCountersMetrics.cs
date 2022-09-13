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

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// .NET EventCounters Instrumentation.
/// </summary>
internal class EventCountersMetrics : EventListener
{
    internal static readonly AssemblyName AssemblyName = typeof(EventCountersMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    private readonly EventCountersInstrumentationOptions options;
    private readonly ConcurrentBag<EventSource> preInitEventSources = new();
    private readonly ConcurrentDictionary<Tuple<string, string>, Instrument> instruments = new();
    private readonly ConcurrentDictionary<Tuple<string, string>, double> values = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountersMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public EventCountersMetrics(EventCountersInstrumentationOptions options)
    {
        this.options = options;

        while (this.preInitEventSources.TryTake(out EventSource source))
        {
            if (this.options.ShouldListenToSource(source.Name))
            {
                this.EnableEvents(source, EventLevel.LogAlways, EventKeywords.None, this.options.EnableEventsArguments);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (this.options == null)
        {
            this.preInitEventSources.Add(source);
        }
        else if (this.options.ShouldListenToSource(source.Name))
        {
            this.EnableEvents(source, EventLevel.LogAlways, EventKeywords.None, this.options.EnableEventsArguments);
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (this.options == null)
        {
            return;
        }

        var payload = eventData.Payload[0] as IDictionary<string, object>;
        var name = payload["Name"].ToString();
        var isGauge = payload.ContainsKey("Mean");
        Tuple<string, string> metricKey = new(eventData.EventSource.Name, name);

        /*if (isGauge)
        {
            this.values[metricKey] = Convert.ToDouble(payload["Mean"]);
        }
        else
        {
            this.values[metricKey] += Convert.ToDouble(payload["Increment"]) / this.options.RefreshIntervalSecs;
        }*/
        this.values[metricKey] = isGauge
            ? Convert.ToDouble(payload["Mean"])
            : Convert.ToDouble(payload["Increment"]) / this.options.RefreshIntervalSecs;

        _ = this.instruments.TryAdd(
            metricKey,
            isGauge ? MeterInstance.CreateObservableGauge(name, () => this.values[metricKey]) : MeterInstance.CreateObservableCounter(name, () => this.values[metricKey]));
    }
}

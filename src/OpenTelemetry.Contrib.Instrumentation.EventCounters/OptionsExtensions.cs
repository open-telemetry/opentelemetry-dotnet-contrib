// <copyright file="OptionsExtensions.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Contrib.Instrumentation.EventCounters;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods for the event counters options.
    /// </summary>
    public static class OptionsExtensions
    {
        /// <summary>
        /// Determines whether an event source was already added.
        /// </summary>
        /// <param name="options">The options to check for the event source.</param>
        /// <param name="eventSource">Name of the event source to check for existence.</param>
        /// <returns>True if the event source was already added, otherwise false.</returns>
        public static bool HasEventSource(this EventCountersOptions options, string eventSource)
        {
            return options.Sources.Any(provider => provider.EventSourceName.Equals(eventSource, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a custom event source.
        /// </summary>
        /// <param name="options">The options to add the event source to.</param>
        /// <param name="eventSourceName">Name of the event source.</param>
        /// <returns>The options instance.</returns>
        public static EventSourceOption AddEventSource(this EventCountersOptions options, string eventSourceName)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(eventSourceName))
            {
                throw new ArgumentNullException(nameof(eventSourceName));
            }

            if (options.HasEventSource(eventSourceName))
            {
                throw new ArgumentException($"Event source '{eventSourceName}' was already added.", nameof(eventSourceName));
            }

            var eventSource = new EventSourceOption { EventSourceName = eventSourceName };

            options.Sources.Add(eventSource);
            return eventSource;
        }

        /// <summary>
        /// Add named event counters to the event source option.
        /// </summary>
        /// <param name="eventSource">The option to add the event counters to.</param>
        /// <param name="counterNames">Name of the counters to listen to.</param>
        /// <returns>The event source instance to define event counters.</returns>
        public static EventSourceOption WithCounters(this EventSourceOption eventSource, params string[] counterNames)
        {
            if (counterNames != null && counterNames.Length > 0)
            {
                eventSource.EventCounters.AddRange(counterNames.Select(name => new EventCounter { Name = name }));
            }

            return eventSource;
        }

        /// <summary>
        /// Adds an event counter to the given event source options.
        /// </summary>
        /// <param name="eventSource">The option to add the event counter to.</param>
        /// <param name="counterName">Name of the event counter.</param>
        /// <param name="description">The metric description.</param>
        /// <param name="instrumentationType">The type of the instrumentation that will be created.</param>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The event source instance to define event counters.</returns>
        public static EventSourceOption With(this EventSourceOption eventSource, string counterName, string description, InstrumentationType instrumentationType, string? metricName = null)
        {
            eventSource.EventCounters.Add(new EventCounter
            {
                Name = counterName,
                Description = description,
                Type = instrumentationType,
                MetricName = metricName,
            });

            return eventSource;
        }
    }
}

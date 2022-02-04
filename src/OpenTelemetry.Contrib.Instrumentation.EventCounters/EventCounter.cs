// <copyright file="EventCounter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters
{
    /// <summary>
    /// Class to describe an event counter.
    /// </summary>
    public class EventCounter
    {
        /// <summary>
        /// Gets or sets the event counter name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the metric description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the instrumentation type.
        /// </summary>
        public InstrumentationType? Type { get; set; } = InstrumentationType.Counter;

        /// <summary>
        /// Gets or sets the name used for the published metric.
        /// </summary>
        public string? MetricName { get; set; }
    }
}

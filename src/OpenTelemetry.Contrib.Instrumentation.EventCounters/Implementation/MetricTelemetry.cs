// <copyright file="MetricTelemetry.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    [DebuggerDisplay("{Name} ({EventSource})")]
    internal class MetricTelemetry
    {
        /// <summary>
        /// Gets or sets the name of the metric.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the event source of the metric.
        /// </summary>
        public string EventSource { get; set; }

        /// <summary>
        /// Gets or sets the values of the metric.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the instrumentation type.
        /// </summary>
        public InstrumentationType Type { get; set; } = InstrumentationType.ObservableCounter;
    }
}

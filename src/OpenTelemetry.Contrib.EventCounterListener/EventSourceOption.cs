// <copyright file="MySqlDataInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounterListener
{
    /// <summary>
    /// Options object for EventSource.
    /// </summary>
    public class EventSourceOption
    {
        /// <summary>
        /// Gets or sets the name of the event source.
        /// </summary>
        public string EventSourceName { get; set; }

        /// <summary>
        /// Gets or sets the Refresh interval in seconds.
        /// </summary>
        public string RefreshInterval { get; set; } = "1";

        /// <summary>
        /// Gets or sets EventCounters.
        /// </summary>
        public EventCounter[] EventCounters { get; set; } = new EventCounter[] { new EventCounter { Name = "System.Runtime", Type = "ObservableGuage" } };

    }
}

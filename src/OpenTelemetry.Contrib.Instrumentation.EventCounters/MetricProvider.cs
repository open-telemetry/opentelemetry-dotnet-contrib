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

using System.Collections.Generic;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters
{
    /// <summary>
    /// The Event Counter to listen to.
    /// </summary>
    public class MetricProvider
    {
        /// <summary>
        /// Gets or sets the name of the event source.
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// Gets or sets the counter names of the event source which should be retrieved.
        /// </summary>
        public List<string> CounterNames { get; set; } = new List<string>(0);
    }
}

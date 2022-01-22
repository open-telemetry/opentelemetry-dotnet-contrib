// <copyright file="EventCounterListenerOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters
{
    /// <summary>
    /// Options for <see cref="EventCounterListener"/>.
    /// </summary>
    public class EventCounterListenerOptions
    {
        /// <summary>
        /// Gets or sets the interval in seconds.
        /// </summary>
        public int RefreshIntervalSecs { get; set; } = 1;

        /// <summary>
        /// Gets or sets metric providers to listen to.
        /// </summary>
        public List<MetricProvider> Providers { get; set; } = new List<MetricProvider>(0);

        /// <summary>
        /// Gets or sets the delegate to create a custom metric name.
        /// </summary>
        public Func<string, string, string>? MetricNameMapper { get; set; }
    }
}

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

using System.Collections.Generic;

namespace OpenTelemetry.Instrumentation.EventCounters;

/// <summary>
/// EventCounterListener Options.
/// </summary>
public class EventCounterListenerOptions
{
    /// <summary>
    /// Gets or sets the subscription interval in seconds.
    /// </summary>
    public int RefreshIntervalSecs { get; set; } = 60;

    /// <summary>
    /// Gets or sets the names of <c>EventSource</c>s to listen to.
    /// </summary>
    public HashSet<string> Sources { get; set; } = new();

    /// <summary>
    /// Gets or sets the name of <c>EventCounters</c> to listen to.
    /// </summary>
    public HashSet<string> Names { get; set; } = new() { "EventCounters" };

    /// <summary>
    /// Gets the arguments object used for the EventListener.EnableEvents function.
    /// </summary>
    public Dictionary<string, string> EnableEventsArguments => new() { { "EventCounterIntervalSec", this.RefreshIntervalSecs.ToString() } };
}

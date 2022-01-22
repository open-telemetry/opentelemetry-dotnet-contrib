// <copyright file="ISystemRuntimeBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.SystemRuntime
{
    /// <summary>
    /// Event source option builder for System.Runtime event counters.
    /// </summary>
    public interface ISystemRuntimeBuilder
    {
        /// <summary>
        /// Add several event counters to the options.
        /// </summary>
        /// <param name="counterNames">Name of the counters to listen to.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder With(params string[] counterNames);

        /// <summary>
        /// Adds the event counter for the percent of time in GC since the last GC.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithPercentOfTimeinGCSinceLastGC(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes allocated per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithAllocationRate(string? metricName = null);
    }
}

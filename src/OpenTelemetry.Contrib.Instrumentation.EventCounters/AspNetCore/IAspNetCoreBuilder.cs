// <copyright file="IAspNetCoreBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.AspNetCore
{
    /// <summary>
    /// Event source option builder for AspNetCore event counters.
    /// </summary>
    public interface IAspNetCoreBuilder
    {
        /// <summary>
        /// Add several event counters to the options.
        /// </summary>
        /// <param name="counterNames">Name of the counters to listen to.</param>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithCounters(params string[] counterNames);

        /// <summary>
        /// Add all known event counters to the options.
        /// </summary>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithAll();

        /// <summary>
        /// Adds the event counter for the total number of requests that have started, but not yet stopped.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithCurrentRequests(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the total number of failed requests that have occurred for the life of the app.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithFailedRequests(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of requests that occur per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithRequestRate(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the total number of requests that have occurred for the life of the app.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        IAspNetCoreBuilder WithTotalRequests(string? metricName = null);
    }
}

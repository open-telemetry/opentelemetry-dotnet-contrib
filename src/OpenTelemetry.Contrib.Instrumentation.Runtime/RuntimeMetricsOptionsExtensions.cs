// <copyright file="RuntimeMetricsOptionsExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Contrib.Instrumentation.Runtime;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extensions to configure the runtime metrics options.
    /// </summary>
    public static class RuntimeMetricsOptionsExtensions
    {
        /// <summary>
        /// Enable the Garbage collection metrics.
        /// </summary>
        /// <param name="options">The options to enable the metrics on.</param>
        /// <returns>The options instance.</returns>
        public static RuntimeMetricsOptions WithGc(this RuntimeMetricsOptions options)
        {
            options.GcEnabled = true;
            return options;
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Enable the Jitter metrics.
        /// </summary>
        /// <param name="options">The options to enable the metrics on.</param>
        /// <returns>The options instance.</returns>
        public static RuntimeMetricsOptions WithJit(this RuntimeMetricsOptions options)
        {
            options.JitEnabled = true;
            return options;
        }
#endif

        /// <summary>
        /// Enable the threading metrics.
        /// </summary>
        /// <param name="options">The options to enable the metrics on.</param>
        /// <returns>The options instance.</returns>
        public static RuntimeMetricsOptions WithThreading(this RuntimeMetricsOptions options)
        {
            options.ThreadingEnabled = true;
            return options;
        }

        /// <summary>
        /// Enable the performance metrics.
        /// </summary>
        /// <param name="options">The options to enable the metrics on.</param>
        /// <returns>The options instance.</returns>
        public static RuntimeMetricsOptions WithPerformance(this RuntimeMetricsOptions options)
        {
            options.PerformanceEnabled = true;
            return options;
        }

        /// <summary>
        /// Enable the assembly metrics.
        /// </summary>
        /// <param name="options">The options to enable the metrics on.</param>
        /// <returns>The options instance.</returns>
        public static RuntimeMetricsOptions WithAssemblies(this RuntimeMetricsOptions options)
        {
            options.AssembliesEnabled = true;
            return options;
        }
    }
}

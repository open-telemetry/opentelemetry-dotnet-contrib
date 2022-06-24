// <copyright file="RuntimeMetricsOptions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Instrumentation.Runtime.Options;

namespace OpenTelemetry.Instrumentation.Runtime
{
    /// <summary>
    /// Options to define the runtime metrics.
    /// </summary>
    public class RuntimeMetricsOptions
    {
        /// <summary>
        /// Gets or sets flags on whether each garbage collection metrics should be collected.
        /// </summary>
        public GcMetricOptions? GcMetricOption { get; set; }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets flags on whether each jitter metrics should be collected.
        /// </summary>
        public JitMetricOptions? JitMetricOption { get; set; }
#endif

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Gets or sets flags on whether each threading metrics should be collected.
        /// </summary>
        public ThreadingMetricOptions? ThreadingMetricOption { get; set; }
#endif

        /// <summary>
        /// Gets or sets flags on whether each assembly metrics should be collected.
        /// </summary>
        public AssemblyMetricOptions? AssemblyMetricOption { get; set; }

        /// <summary>
        /// Gets or sets flags on whether each exception metrics should be collected.
        /// </summary>
        public ExceptionMetricOptions? ExceptionMetricOption { get; set; }

        /// <summary>
        /// Gets a value indicating whether all metrics are enabled.
        /// </summary>
        internal bool IsDefault => this.GcMetricOption == null
#if NET6_0_OR_GREATER
        && this.JitMetricOption == null
#endif
#if NETCOREAPP3_1_OR_GREATER
        && this.ThreadingMetricOption == null
#endif
        && this.AssemblyMetricOption == null
        && this.ExceptionMetricOption == null;

        /// <summary>
        /// Gets a value indicating whether garbage collection metrics is enabled.
        /// </summary>
        internal GcMetricOptions GetGcOption => this.IsDefault ? GcMetricOptions.All : this.GcMetricOption.GetValueOrDefault(GcMetricOptions.None);

#if NET6_0_OR_GREATER
        /// <summary>
        /// Gets a value indicating whether jitter metrics is enabled.
        /// </summary>
        internal JitMetricOptions GetJitOption => this.IsDefault ? JitMetricOptions.All : this.JitMetricOption.GetValueOrDefault(JitMetricOptions.None);
#endif

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Gets a value indicating whether threading metrics is enabled.
        /// </summary>
        internal ThreadingMetricOptions GetThreadingOption => this.IsDefault ? ThreadingMetricOptions.All : this.ThreadingMetricOption.GetValueOrDefault(ThreadingMetricOptions.None);
#endif

        /// <summary>
        /// Gets a value indicating whether assembly metrics is enabled.
        /// </summary>
        internal AssemblyMetricOptions GetAssemblyOption => this.IsDefault ? AssemblyMetricOptions.All : this.AssemblyMetricOption.GetValueOrDefault(AssemblyMetricOptions.None);

        /// <summary>
        /// Gets a value indicating whether exception count metrics is enabled.
        /// </summary>
        internal ExceptionMetricOptions GetExceptionOption => this.IsDefault ? ExceptionMetricOptions.All : this.ExceptionMetricOption.GetValueOrDefault(ExceptionMetricOptions.None);
    }
}

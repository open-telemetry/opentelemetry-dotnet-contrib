// <copyright file="RuntimeMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime
{
    /// <summary>
    /// .NET runtime instrumentation.
    /// </summary>
    internal class RuntimeMetrics : IDisposable
    {
        internal static readonly AssemblyName AssemblyName = typeof(RuntimeMetrics).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly Meter meter;
        private readonly List<IRuntimeInstrumentation> instrumentations = new List<IRuntimeInstrumentation>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeMetrics"/> class.
        /// </summary>
        /// <param name="options">The options to define the metrics.</param>
        public RuntimeMetrics(RuntimeMetricsOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);

            if (options.IsGcEnabled)
            {
                this.instrumentations.Add(new GcInstrumentation(options, this.meter));
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.instrumentations.Add(new JitInstrumentation(options, this.meter));
            }
#endif

            if (options.IsThreadingEnabled)
            {
                this.instrumentations.Add(new ThreadingInstrumentation(options, this.meter));
            }

            if (options.IsPerformanceEnabled)
            {
                this.instrumentations.Add(new PerformanceInstrumentation(options, this.meter));
            }

            if (options.IsAssembliesEnabled)
            {
                this.instrumentations.Add(new AssembliesInstrumentation(options, this.meter));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.meter?.Dispose();
        }
    }
}

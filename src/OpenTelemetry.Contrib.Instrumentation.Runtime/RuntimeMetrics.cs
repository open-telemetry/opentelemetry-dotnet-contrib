﻿// <copyright file="RuntimeMetrics.cs" company="OpenTelemetry Authors">
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
        private readonly EventCounterListener eventCounterListener;
        private readonly EventCounterStore eventCounterStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeMetrics"/> class.
        /// </summary>
        /// <param name="options">The options to define the metrics.</param>
        public RuntimeMetrics(RuntimeMetricsOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.eventCounterStore = new EventCounterStore();

            if (options.IsGcEnabled)
            {
                this.instrumentations.Add(new GcInstrumentation(this.meter, this.eventCounterStore));
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.instrumentations.Add(new JitInstrumentation(this.meter));
            }
#endif

            if (options.IsThreadingEnabled)
            {
                this.instrumentations.Add(new ThreadingInstrumentation(this.meter));
            }

            if (options.IsPerformanceEnabled)
            {
                this.instrumentations.Add(new PerformanceInstrumentation(this.meter, this.eventCounterStore));
            }

            if (options.IsExceptionsEnabled)
            {
                this.instrumentations.Add(new ExceptionsInstrumentation(this.meter, this.eventCounterStore));
            }

            if (options.IsAssembliesEnabled)
            {
                this.instrumentations.Add(new AssembliesInstrumentation(this.meter));
            }

            if (this.eventCounterStore.HasSubscriptions())
            {
                this.eventCounterListener = new EventCounterListener(this.eventCounterStore);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var instrumentation in this.instrumentations)
            {
                if (instrumentation is IDisposable disposableInstrumentation)
                {
                    disposableInstrumentation.Dispose();
                }
            }

            this.meter?.Dispose();
            this.eventCounterListener?.Dispose();
        }
    }
}

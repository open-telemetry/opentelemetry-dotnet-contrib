// <copyright file="JitInstrumentation.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class JitInstrumentation : IRuntimeInstrumentation
    {
        private readonly Meter meter;
        private readonly ObservableCounter<long> ilBytesJittedCounter;
        private readonly ObservableCounter<long> methodsJittedCounter;
        private readonly ObservableGauge<double> jitTimeCounter;

        public JitInstrumentation(Meter meter)
        {
            this.meter = meter;
            this.ilBytesJittedCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}il_bytes_jitted", () => System.Runtime.JitInfo.GetCompiledILBytes(), "B", description: "IL Bytes Jitted");
            this.methodsJittedCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}methods_jitted_count", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted");
            this.jitTimeCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}time_in_jit", () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, "ms", description: "Time spent in JIT");
        }
    }
}
#endif

// <copyright file="GcInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class GcInstrumentation : IRuntimeInstrumentation
    {
        public GcInstrumentation(RuntimeMetricsOptions options, Meter meter)
        {
            meter.CreateObservableGauge($"{options.MetricPrefix}gc_heap_size", () => (double)(GC.GetTotalMemory(false) / 1_000_000), "MB", "GC Heap Size");
            meter.CreateObservableGauge($"{options.MetricPrefix}gen_0-gc_count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
            meter.CreateObservableGauge($"{options.MetricPrefix}gen_1-gc_count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
            meter.CreateObservableGauge($"{options.MetricPrefix}gen_2-gc_count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
            meter.CreateObservableCounter($"{options.MetricPrefix}alloc_rate", () => GC.GetTotalAllocatedBytes(), "B", "Allocation Rate");
            meter.CreateObservableCounter($"{options.MetricPrefix}gc_fragmentation", GetFragmentation, description: "GC Fragmentation");

#if NET6_0_OR_GREATER
            meter.CreateObservableCounter($"{options.MetricPrefix}gc_committed", () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000), "MB", description: "GC Committed Bytes");
#endif
        }

        private static double GetFragmentation()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
        }
    }
}

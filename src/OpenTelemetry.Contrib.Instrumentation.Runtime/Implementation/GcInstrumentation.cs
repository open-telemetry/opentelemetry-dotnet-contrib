﻿// <copyright file="GcInstrumentation.cs" company="OpenTelemetry Authors">
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
    internal class GcInstrumentation : IRuntimeInstrumentation, IDisposable
    {
        private const string CommittedCounterName = "gc-committed";
        private const string GcTimeCounterName = "time-in-gc";
        private const string Gen0SizeCounterName = "gen-0-size";
        private const string Gen1SizeCounterName = "gen-1-size";
        private const string Gen2SizeCounterName = "gen-2-size";
        private const string LohSizeCounterName = "loh-size";
        private const string PohSizeCounterName = "poh-size";

        private readonly Meter meter;
        private readonly IEventCounterStore eventCounterStore;
        private readonly ObservableCounter<double> gcHeapSizeCounter;
        private readonly ObservableGauge<int> gen0GCCounter;
        private readonly ObservableGauge<int> gen1GCCounter;
        private readonly ObservableGauge<int> gen2GCCounter;
        private readonly ObservableGauge<long> allocRateCounter;
        private readonly ObservableCounter<double> fragmentationCounter;
        private readonly ObservableCounter<double> committedCounter;
        private readonly ObservableCounter<double> gcTimeCounter;
        private readonly ObservableCounter<long> gen0SizeCounter;
        private readonly ObservableCounter<long> gen1SizeCounter;
        private readonly ObservableCounter<long> gen2SizeCounter;
        private readonly ObservableCounter<long> lohSizeCounter;
        private readonly ObservableCounter<long> pohSizeCounter;

        public GcInstrumentation(Meter meter, IEventCounterStore eventCounterStore)
        {
            this.meter = meter;
            this.eventCounterStore = eventCounterStore;

            this.eventCounterStore.Subscribe(GcTimeCounterName, EventCounterType.Mean);
            this.eventCounterStore.Subscribe(Gen0SizeCounterName, EventCounterType.Mean);
            this.eventCounterStore.Subscribe(Gen1SizeCounterName, EventCounterType.Mean);
            this.eventCounterStore.Subscribe(Gen2SizeCounterName, EventCounterType.Mean);
            this.eventCounterStore.Subscribe(LohSizeCounterName, EventCounterType.Mean);
            this.eventCounterStore.Subscribe(PohSizeCounterName, EventCounterType.Mean);

            this.gcHeapSizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gc_heap_size", () => (double)(GC.GetTotalMemory(false) / 1_000_000), "MB", "GC Heap Size");
            this.gen0GCCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}gen_0-gc_count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
            this.gen1GCCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}gen_1-gc_count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
            this.gen2GCCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}gen_2-gc_count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
            this.allocRateCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}alloc_rate", () => GC.GetTotalAllocatedBytes(), "B", "Allocation Rate");
            this.fragmentationCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gc_fragmentation", GetFragmentation, description: "GC Fragmentation");

#if NET6_0_OR_GREATER
            this.committedCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gc_committed", () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000), "MB", description: "GC Committed Bytes");
#else
            this.eventCounterStore.Subscribe(CommittedCounterName, EventCounterType.Mean);
            this.committedCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gc_committed", () => this.eventCounterStore.ReadDouble(CommittedCounterName), "MB", description: "GC Committed Bytes");
#endif

            this.gcTimeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}time_in_gc", () => this.eventCounterStore.ReadDouble(GcTimeCounterName), description: "% Time in GC since last GC");
            this.gen0SizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gen_0_size", () => this.eventCounterStore.ReadLong(Gen0SizeCounterName), "B", description: "Gen 0 Size");
            this.gen1SizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gen_1_size", () => this.eventCounterStore.ReadLong(Gen1SizeCounterName), "B", description: "Gen 1 Size");
            this.gen2SizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}gen_2_size", () => this.eventCounterStore.ReadLong(Gen2SizeCounterName), "B", description: "Gen 2 Size");
            this.lohSizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}loh_size", () => this.eventCounterStore.ReadLong(LohSizeCounterName), "B", description: "LOH Size");
            this.pohSizeCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}poh_size", () => this.eventCounterStore.ReadLong(PohSizeCounterName), "B", description: "POH (Pinned Object Heap) Size");
        }

        public void Dispose()
        {
            this.eventCounterStore?.Unsubscribe(GcTimeCounterName);
            this.eventCounterStore?.Unsubscribe(Gen0SizeCounterName);
            this.eventCounterStore?.Unsubscribe(Gen1SizeCounterName);
            this.eventCounterStore?.Unsubscribe(Gen2SizeCounterName);
            this.eventCounterStore?.Unsubscribe(LohSizeCounterName);
            this.eventCounterStore?.Unsubscribe(PohSizeCounterName);

#if !NET6_0_OR_GREATER
            this.eventCounterStore?.Unsubscribe(CommittedCounterName);
#endif
        }

        private static double GetFragmentation()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
        }
    }
}

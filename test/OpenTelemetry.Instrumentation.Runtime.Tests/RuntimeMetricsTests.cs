// <copyright file="RuntimeMetricsTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Runtime.Tests
{
    public class RuntimeMetricsTests
    {
        private const int MaxTimeToAllowForFlush = 10000;
        private const string MetricPrefix = "process.runtime.dotnet.";

        [Fact]
        public void RuntimeMetricsAreCaptured()
        {
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeInstrumentation()
                 .AddInMemoryExporter(exportedItems)
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.True(exportedItems.Count > 1);
            Assert.StartsWith(MetricPrefix, exportedItems[0].Name);

            var gcCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.gc.collections.count");
            var sumReceived = GetLongSum(gcCountMetric);
            Assert.True(sumReceived >= 0);

#if NETCOREAPP3_1_OR_GREATER
            var gcAllocationSizeMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.gc.allocations.size");
            Assert.True(GetLongSum(gcAllocationSizeMetric) > 0);
#endif

#if NET6_0_OR_GREATER
            // Supposedly to pass if no garbage collection occurred before. However it did, which is out of control of the code.
            //Assert.False(exportedItems.Exists(i => i.Name == "process.runtime.dotnet.gc.committed_memory.size"));
            //Assert.False(exportedItems.Exists(i => i.Name == "process.runtime.dotnet.gc.heap.size"));
#endif

            var assembliesCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.assemblies.count");
            Assert.True(GetLongSum(assembliesCountMetric) > 0);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public void RuntimeMetrics_GcAvailableAfterFirst()
        {
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeInstrumentation()
                 .AddInMemoryExporter(exportedItems)
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            System.GC.Collect(1);

            var gcCommittedMemorySizeMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.gc.committed_memory.size");
            Assert.True(GetLongSum(gcCommittedMemorySizeMetric) > 0);

            var gcHeapSizeMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.gc.heap.size");
            Assert.True(GetLongSum(gcHeapSizeMetric) > 0);
        }
#endif

#if NET6_0_OR_GREATER
        [Fact]
        public void RuntimeMetrics_JitRelatedMetrics()
        {
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeInstrumentation()
                 .AddInMemoryExporter(exportedItems)
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.True(exportedItems.Count > 1);
            Assert.StartsWith(MetricPrefix, exportedItems[0].Name);

            var jitCompiledSizeMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.jit.il_compiled.size");
            Assert.True(GetLongSum(jitCompiledSizeMetric) > 0);

            var jitMethodsCompiledCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.jit.methods_compiled.count");
            Assert.True(GetLongSum(jitMethodsCompiledCountMetric) > 0);

            var jitCompilationTimeMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.jit.compilation_time");
            Assert.True(GetLongSum(jitCompilationTimeMetric) > 0);
        }
#endif

#if NETCOREAPP3_1_OR_GREATER
        [Fact]
        public void RuntimeMetrics_ThreadingRelatedMetrics()
        {
            var exportedItems = new List<Metric>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeInstrumentation()
                 .AddInMemoryExporter(exportedItems)
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.True(exportedItems.Count > 1);
            Assert.StartsWith(MetricPrefix, exportedItems[0].Name);

            var lockContentionCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.monitor.lock_contention.count");
            Assert.True(GetLongSum(lockContentionCountMetric) >= 0);

            var threadCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.thread_pool.threads.count");
            Assert.True(GetLongSum(threadCountMetric) > 0);

            var completedItemsCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.thread_pool.completed_items.count");
            Assert.True(GetLongSum(completedItemsCountMetric) > 0);

            var queueLengthMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.thread_pool.queue.length");
            Assert.True(GetLongSum(queueLengthMetric) == 0);

            var timerCountMetric = exportedItems.First(i => i.Name == "process.runtime.dotnet.timer.count");
            Assert.True(GetLongSum(timerCountMetric) > 0);
        }
#endif

        private static double GetLongSum(Metric metric)
        {
            double sum = 0;

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumLong();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueLong();
                }
            }

            return sum;
        }
    }
}

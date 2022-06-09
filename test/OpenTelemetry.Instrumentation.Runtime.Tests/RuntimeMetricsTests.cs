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

using System;
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
                 .AddRuntimeMetrics(options =>
                 {
                     options.GcEnabled = true;
#if NETCOREAPP3_1_OR_GREATER
                     options.ThreadingEnabled = true;
#endif
                     options.ProcessEnabled = true;
#if NET6_0_OR_GREATER

                     options.JitEnabled = true;
#endif
                     options.AssembliesEnabled = true;
                 })
                 .AddInMemoryExporter(exportedItems)
                .Build();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            Assert.True(exportedItems.Count > 1);
            var metric1 = exportedItems[0];
            Assert.StartsWith(MetricPrefix, metric1.Name);
        }

        [Fact]
        public void ProcessMetricsAreCaptured()
        {
            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeMetrics(options =>
                 {
                     options.ProcessEnabled = true;
                 })
                 .AddInMemoryExporter(exportedItems)
                .Build();

            // simple CPU spinning
            var spinDuration = DateTime.UtcNow.AddMilliseconds(10);
            while (DateTime.UtcNow < spinDuration)
            {
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.Equal(4, exportedItems.Count);

            var cpuTimeMetric = exportedItems.First(i => i.Name == "process.cpu.time");
            var sumReceived = GetLongSum(cpuTimeMetric);
            Assert.True(sumReceived > 0);

            var cpuCountMetric = exportedItems.First(i => i.Name == "process.cpu.count");
            Assert.Equal(Environment.ProcessorCount, (int)GetLongSum(cpuCountMetric));

            var memoryMetric = exportedItems.First(i => i.Name == "process.memory.usage");
            Assert.True(GetLongSum(memoryMetric) > 0);

            var virtualMemoryMetric = exportedItems.First(i => i.Name == "process.memory.virtual");
            Assert.True(GetLongSum(virtualMemoryMetric) > 0);
        }

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

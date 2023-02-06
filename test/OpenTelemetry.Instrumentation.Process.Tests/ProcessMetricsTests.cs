// <copyright file="ProcessMetricsTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Process.Tests;

public class ProcessMetricsTests
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Fact]
    public void ProcessMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.True(exportedItems.Count == 4);
        var physicalMemoryMetric = exportedItems.FirstOrDefault(i => i.Name == "process.memory.usage");
        Assert.NotNull(physicalMemoryMetric);
        var virtualMemoryMetric = exportedItems.FirstOrDefault(i => i.Name == "process.memory.virtual");
        Assert.NotNull(virtualMemoryMetric);
        var cpuTimeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.cpu.time");
        Assert.NotNull(cpuTimeMetric);
        var threadMetric = exportedItems.FirstOrDefault(i => i.Name == "process.threads");
        Assert.NotNull(threadMetric);
    }

    [Fact]
    public void CpuTimeMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var cpuTimeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.cpu.time");
        Assert.NotNull(cpuTimeMetric);

        var userTimeCaptured = false;
        var systemTimeCaptured = false;

        var points = cpuTimeMetric.GetMetricPoints().GetEnumerator();
        while (points.MoveNext() && (!userTimeCaptured || !systemTimeCaptured))
        {
            foreach (var tag in points.Current.Tags)
            {
                if (tag.Key == "state" && tag.Value.ToString() == "user")
                {
                    userTimeCaptured = true;
                }
                else if (tag.Key == "state" && tag.Value.ToString() == "system")
                {
                    systemTimeCaptured = true;
                }
            }
        }

        Assert.True(userTimeCaptured);
        Assert.True(systemTimeCaptured);
    }

    [Fact]
    public void CheckValidGaugeValueWhen2MeterProviderInstancesHaveTheSameMeterName()
    {
        var exportedItemsA = new List<Metric>();
        var exportedItemsB = new List<Metric>();

        using var meterProviderA = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItemsA)
            .Build();

        using (var meterProviderB = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItemsB)
            .Build())
        {
            meterProviderA.ForceFlush(MaxTimeToAllowForFlush);
            meterProviderB.ForceFlush(MaxTimeToAllowForFlush);

            var metricA = exportedItemsA.FirstOrDefault(i => i.Name == "process.memory.usage");
            var metricB = exportedItemsB.FirstOrDefault(i => i.Name == "process.memory.usage");

            Assert.NotNull(metricA);
            Assert.NotNull(metricB);

            Assert.True(GetValue(metricA) > 0);
            Assert.True(GetValue(metricB) > 0);
        }

        exportedItemsA.Clear();
        exportedItemsB.Clear();

        meterProviderA.ForceFlush(MaxTimeToAllowForFlush);

        Assert.NotEmpty(exportedItemsA);
        Assert.Empty(exportedItemsB);

        exportedItemsA.Clear();
        exportedItemsB.Clear();

        meterProviderA.ForceFlush(MaxTimeToAllowForFlush);

        Assert.NotEmpty(exportedItemsA);
        Assert.Empty(exportedItemsB);

        exportedItemsA.Clear();

        meterProviderA.ForceFlush(MaxTimeToAllowForFlush);

        Assert.NotEmpty(exportedItemsA);
        Assert.Empty(exportedItemsB);
    }

    private static double GetValue(Metric metric)
    {
        double sum = 0;

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            if (metric.MetricType.IsLong())
            {
                sum += metricPoint.GetSumLong();
            }
        }

        return sum;
    }
}

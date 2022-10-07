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
using System.Diagnostics.Metrics;
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

        Assert.True(exportedItems.Count == 2);
        var physicalMemoryMetric = exportedItems.FirstOrDefault(i => i.Name == "process.memory.usage");
        Assert.NotNull(physicalMemoryMetric);
        var virtualMemoryMetric = exportedItems.FirstOrDefault(i => i.Name == "process.memory.virtual");
        Assert.NotNull(virtualMemoryMetric);
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

        using var meterProviderB = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItemsB)
            .Build();

        meterProviderA.ForceFlush(MaxTimeToAllowForFlush);
        meterProviderB.ForceFlush(MaxTimeToAllowForFlush);

        var metricA = exportedItemsA.FirstOrDefault(i => i.Name == "process.memory.usage");
        var metricB = exportedItemsB.FirstOrDefault(i => i.Name == "process.memory.usage");

        Assert.True(GetValue(metricA) > 0);
        Assert.True(GetValue(metricB) > 0);
    }

    [Fact]
    public void UnifiedMetricStreamIdentityWhen2MeterProviderInstancesHaveTheSameMeterName()
    {
        var exportedItemsA = new List<Metric>();
        var exportedItemsB = new List<Metric>();

        Meter m1 = new("myMeter");
        Meter m2 = new("myMeter");

        m1.CreateObservableCounter(
            "myGaugeName",
            () => { return 1D; },
            unit: "1",
            description: "test");

        m1.CreateObservableCounter(
            "myGaugeName",
            () => { return 2D; },
            unit: "1",
            description: "test");

        using var meterProviderA = Sdk.CreateMeterProviderBuilder()
            .AddMeter("myMeter")
            .AddInMemoryExporter(exportedItemsA)
            .Build();

        using var meterProviderB = Sdk.CreateMeterProviderBuilder()
            .AddMeter("myMeter")
            .AddInMemoryExporter(exportedItemsB)
            .Build();

        meterProviderA.ForceFlush();
        meterProviderB.ForceFlush();

        var metricA = exportedItemsA.FirstOrDefault(i => i.Name == "myGaugeName");
        var metricB = exportedItemsB.FirstOrDefault(i => i.Name == "myGaugeName");

        Assert.Equal(GetValue(metricA), GetValue(metricB));
    }

    private static double GetValue(Metric metric)
    {
        Assert.NotNull(metric);
        double sum = 0;

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            if (metric.MetricType.IsGauge())
            {
                sum += metricPoint.GetGaugeLastValueDouble();
            }
            else if (metric.MetricType.IsDouble())
            {
                sum += metricPoint.GetSumDouble();
            }
        }

        return sum;
    }
}

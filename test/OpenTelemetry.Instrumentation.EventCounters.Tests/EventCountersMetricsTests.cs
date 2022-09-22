// <copyright file="EventCountersMetricsTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

public class EventCountersMetricsTests
{
    private const int Delay = 1200;

    [Fact(Skip = "Other tests metrics are being exported here")]
    public async Task NoMetricsByDefault()
    {
        // Arrange
        List<Metric> metricItems = new();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        Assert.Empty(metricItems);
    }

    [Fact]
    public async Task EventCounter()
    {
        // Arrange
        List<Metric> metricItems = new();
        EventSource source = new("source-a");
        EventCounter counter = new("counter", source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSource(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        counter.WriteMetric(1997.0202);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "counter");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(1997.0202, GetActualValue(metric));
    }

    [Fact]
    public async Task IncrementingEventCounter()
    {
        // Arrange
        List<Metric> metricItems = new();
        EventSource source = new("source-b");
        IncrementingEventCounter incCounter = new("inc-counter", source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSource(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        incCounter.Increment(1);
        incCounter.Increment(1);
        incCounter.Increment(1);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "inc-counter");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(3, GetActualValue(metric));
    }

    [Fact]
    public async Task PollingCounter()
    {
        // Arrange
        int i = 0;
        List<Metric> metricItems = new();
        EventSource source = new("source-c");
        PollingCounter pollCounter = new("poll-counter", source, () => ++i * 10);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSource(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        await Task.Delay(Delay * 2);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "poll-counter");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(20, GetActualValue(metric));
    }

    [Fact]
    public async Task IncrementingPollingCounter()
    {
        // Arrange
        int i = 1;
        List<Metric> metricItems = new();
        EventSource source = new("source-d");
        IncrementingPollingCounter incPollCounter = new("inc-poll-counter", source, () => i++);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSource(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        await Task.Delay(Delay * 2);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "inc-poll-counter");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(2, GetActualValue(metric));
    }

    private static double GetActualValue(Metric metric)
    {
        double sum = 0;

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            sum += metric.MetricType.IsSum()
                ? metricPoint.GetSumDouble()
                : metricPoint.GetGaugeLastValueDouble();
        }

        return sum;
    }
}

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

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

public class EventCountersMetricsTests
{
    [Fact]
    public void NoMetricsByDefault()
    {
        List<Metric> metricItems = new();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        Assert.False(AwaitExport(meterProvider, metricItems));
        Assert.Empty(metricItems);
    }

    [Fact]
    public void EventCounter()
    {
        List<Metric> metricItems = new();
        EventSource source = new("a");
        EventCounter counter = new("c", source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        counter.WriteMetric(1.11);

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems.Find(x => x.Name == "ec.a.c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(1.11, GetActualValue(metric));
    }

    [Fact]
    public void IncrementingEventCounter()
    {
        List<Metric> metricItems = new();
        EventSource source = new("b");
        IncrementingEventCounter incCounter = new("inc-c", source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        incCounter.Increment(1);
        incCounter.Increment(1);
        incCounter.Increment(1);

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems.Find(x => x.Name == "ec.b.inc-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(3, GetActualValue(metric));
    }

    [Fact]
    public void PollingCounter()
    {
        int i = 0;
        List<Metric> metricItems = new();
        EventSource source = new("c");
        PollingCounter pollCounter = new("poll-c", source, () => ++i * 10);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems.Find(x => x.Name == "ec.c.poll-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(20, GetActualValue(metric));
    }

    [Fact]
    public void IncrementingPollingCounter()
    {
        int i = 1;
        List<Metric> metricItems = new();
        EventSource source = new("d");
        IncrementingPollingCounter incPollCounter = new("inc-poll-c", source, () => i++);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems.Find(x => x.Name == "ec.d.inc-poll-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(2, GetActualValue(metric));
    }

    [Fact]
    public void EventCounterSameNameUsesNewestCreated()
    {
        List<Metric> metricItems = new();
        EventSource source = new("a");
        EventCounter counter = new("c", source);
        EventCounter counter2 = new("c", source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        counter.WriteMetric(1.11);
        counter2.WriteMetric(2.22);

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems.Find(x => x.Name == "ec.a.c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);

        // Since `counter2` was created after `counter` it is exported
        Assert.Equal(1.11, GetActualValue(metric));
    }

    [Fact]
    public void ThrowExceptionWhenBuilderIsNull()
    {
        MeterProviderBuilder builder = null;
        Assert.Throws<ArgumentNullException>(() => builder.AddEventCountersInstrumentation());
    }

    [Fact]
    public void ThrowExceptionForUnsupportedEventSources()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
        {
            Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources("System.Runtime");
            });
        });

        Assert.Equal("Use the `OpenTelemetry.Instrumentation.Runtime` or `OpenTelemetry.Instrumentation.Process` instrumentations.", ex.Message);
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

    private static bool AwaitExport<T>(MeterProvider meterProvider, List<T> exportedItems, int count = 1, int sleep = 10, int timeout = 2000)
    {
        return SpinWait.SpinUntil(
            () =>
            {
                Thread.Sleep(sleep);
                meterProvider.ForceFlush();
                return exportedItems.Count == count;
            },
            timeout);
    }
}

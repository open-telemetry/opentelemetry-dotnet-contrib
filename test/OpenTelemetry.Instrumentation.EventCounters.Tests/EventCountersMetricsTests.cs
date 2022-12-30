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

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
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
        using EventSource source = new("a");
        using EventCounter counter = new("c", source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        counter.WriteMetric(1997.0202);

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems[0];
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(1997.0202, GetActualValue(metric));
    }

    [Fact]
    public void IncrementingEventCounter()
    {
        List<Metric> metricItems = new();
        using EventSource source = new("b");
        using IncrementingEventCounter incCounter = new("inc-c", source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
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
        var metric = metricItems[0];
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(3, GetActualValue(metric));
    }

    [Fact]
    public void PollingCounter()
    {
        int i = 0;
        List<Metric> metricItems = new();
        using EventSource source = new("c");
        using PollingCounter pollCounter = new("poll-c", source, () => ++i * 10);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems[0];
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(10, GetActualValue(metric));
    }

    [Fact]
    public void IncrementingPollingCounter()
    {
        int i = 1;
        List<Metric> metricItems = new();
        using EventSource source = new("d");
        using IncrementingPollingCounter incPollCounter = new("inc-poll-c", source, () => i++);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        var metric = metricItems[0];
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(1, GetActualValue(metric));
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThrowExceptionForInvalidRefreshIntervalSet(int refreshIntervalSecs)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.RefreshIntervalSecs = refreshIntervalSecs;
            });
        });

        Assert.Equal("EventCounters Instrumentation `RefreshIntervalSecs` must be >= 1.", ex.Message);
    }

    [Theory]
    [InlineData("Microsoft-AspNetCore-Server-Kestrel-1", "tls-handshakes-per-second", "ec.Microsoft-AspNetCore-Server-Kestre.tls-handshakes-per-second")]
    [InlineData("Microsoft-AspNetCore-Server-Kestrel-1", "tls-handshakes-per-sec", "ec.Microsoft-AspNetCore-Server-Kestrel-1.tls-handshakes-per-sec")]
    [InlineData("Microsoft.AspNetCore.Http.Connections-1", "connections-stopped", "ec.Microsoft.AspNetCore.Http.Connections-1.connections-stopped")]
    [InlineData("Microsoft.AspNetCore.Http.Connections-1", "connections-timed-out-longer", "ec.Microsoft.AspNetCore.Http.Conne.connections-timed-out-longer")]
    [InlineData("Microsoft.AspNetCore.Http.Conn.Something", "connections-timed-out-longer", "ec.Microsoft.AspNetCore.Http.Conn.connections-timed-out-longer")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-very-very-long-event-name", "ec.very-very-very-very-very-very-very-very-very-long-event-name")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-very-long-event-name", "ec.Micr.very-very-very-very-very-very-very-very-long-event-name")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-long-event-name", "ec.Microsoft.very-very-very-very-very-very-very-long-event-name")]
    public void EventSourceNameShortening(string sourceName, string eventName, string expectedInstrumentName)
    {
        List<Metric> metricItems = new();
        using EventSource source = new(sourceName);
        using IncrementingEventCounter connections = new(eventName, source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        connections.Increment(1);

        Assert.True(AwaitExport(meterProvider, metricItems));
        Assert.Single(metricItems);
        Metric metric = metricItems[0];
        Assert.NotNull(metric);
        Assert.Equal(expectedInstrumentName, metric.Name);
        Assert.Equal(1, GetActualValue(metric));
    }

    [Fact]
    public void InstrumentNameTooLong()
    {
        List<Metric> metricItems = new();
        using EventSource source = new("source");

        // ec.s. + event name is 63;
        string veryLongEventName = new string('e', 100);
        using IncrementingEventCounter connections = new(veryLongEventName, source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        connections.Increment(1);
        Assert.True(AwaitExport(meterProvider, metricItems));

        Assert.Single(metricItems);
        Assert.False(metricItems[0].Name.StartsWith("ec.source.ee"));
        Assert.False(metricItems[0].Name.StartsWith("ec.s.ee"));
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

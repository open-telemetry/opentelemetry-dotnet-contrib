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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

public class EventCountersMetricsTests
{
    [Fact(Skip = "Unstable")]
    public void EventCounter()
    {
        // Arrange
        List<Metric> metricItems = new();
        using EventSource source = new("a");
        using EventCounter counter = new("c", source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build()!;

        // Act
        counter.WriteMetric(1997.0202);
        var metric = AwaitExport(meterProvider, metricItems, expectedInstrumentName: "ec.a.c");

        // Assert
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(1997.0202, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public void IncrementingEventCounter()
    {
        // Arrange
        List<Metric> metricItems = new();
        using EventSource source = new("b");
        using IncrementingEventCounter incCounter = new("inc-c", source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build()!;

        // Act
        incCounter.Increment(1);
        incCounter.Increment(1);
        incCounter.Increment(1);
        var metric = AwaitExport(meterProvider, metricItems, expectedInstrumentName: "ec.b.inc-c");

        // Assert
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(3, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public void PollingCounter()
    {
        // Arrange
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
            .Build()!;

        // Act
        var metric = AwaitExport(meterProvider, metricItems, expectedInstrumentName: "ec.c.poll-c");

        // Assert
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(10, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public void IncrementingPollingCounter()
    {
        // Arrange
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
            .Build()!;

        // Act
        var metric = AwaitExport(meterProvider, metricItems, expectedInstrumentName: "ec.d.inc-poll-c");

        // Assert
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(1, GetActualValue(metric));
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
        // Arrange
        List<Metric> metricItems = new();
        using EventSource source = new(sourceName);
        using IncrementingEventCounter connections = new(eventName, source);

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build()!;

        // Act
        connections.Increment(1);
        var metric = AwaitExport(meterProvider, metricItems, expectedInstrumentName);

        // Assert
        Assert.NotNull(metric);
        Assert.Equal(1, GetActualValue(metric));
    }

    [Fact(Skip = "This test should properly validate no metrics are exported from event counters with invalid names (too long)")]
    public void InstrumentNameTooLong()
    {
        // Arrange
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

        // Act
        connections.Increment(1);

        Task.Delay(1800).Wait();
        meterProvider.ForceFlush();

        // Assert
        foreach (var item in metricItems)
        {
            Assert.False(item.Name.StartsWith("ec.source.ee", StringComparison.Ordinal));
            Assert.False(item.Name.StartsWith("ec.s.ee", StringComparison.Ordinal));
        }
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

    private static Metric? AwaitExport(MeterProvider meterProvider, List<Metric> exportedItems, string expectedInstrumentName)
    {
        Metric? metric = null;

        SpinWait.SpinUntil(
            () =>
            {
                Thread.Sleep(100);
                meterProvider.ForceFlush();
                metric = exportedItems.FirstOrDefault(x => x.Name == expectedInstrumentName);
                return metric != null;
            },
            10_000);

        return metric;
    }
}

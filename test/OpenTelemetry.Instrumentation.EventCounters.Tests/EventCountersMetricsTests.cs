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

    [Fact(Skip = "Unstable")]
    public async Task EventCounter()
    {
        // Arrange
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

        // Act
        counter.WriteMetric(1997.0202);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "ec.a.c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(1997.0202, GetActualValue(metric));
    }

    [Fact]
    public async Task IncrementingEventCounter()
    {
        // Arrange
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

        // Act
        incCounter.Increment(1);
        incCounter.Increment(1);
        incCounter.Increment(1);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "ec.b.inc-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(3, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public async Task PollingCounter()
    {
        // Arrange
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

        // Act
        await Task.Delay(Delay * 2);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "ec.c.poll-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);
        Assert.Equal(20, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public async Task IncrementingPollingCounter()
    {
        // Arrange
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

        // Act
        await Task.Delay(Delay * 2);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "ec.d.inc-poll-c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleSum, metric.MetricType);
        Assert.Equal(2, GetActualValue(metric));
    }

    [Fact(Skip = "Unstable")]
    public async Task EventCounterSameNameUsesNewestCreated()
    {
        // Arrange
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

        // Act
        counter2.WriteMetric(1980.1208);
        counter.WriteMetric(1997.0202);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        var metric = metricItems.Find(x => x.Name == "ec.a.c");
        Assert.NotNull(metric);
        Assert.Equal(MetricType.DoubleGauge, metric.MetricType);

        // Since `counter2` was created after `counter` it is exported
        Assert.Equal(1980.1208, GetActualValue(metric));
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
    [InlineData("Microsoft-AspNetCore-Server-Kestrel-1", "tls-handshakes-per-second", "ec.Microsoft-AspNetCore-Server-Kestre.tls-handshakes-per-second")]
    [InlineData("Microsoft-AspNetCore-Server-Kestrel-1", "tls-handshakes-per-sec", "ec.Microsoft-AspNetCore-Server-Kestrel-1.tls-handshakes-per-sec")]
    [InlineData("Microsoft.AspNetCore.Http.Connections-1", "connections-stopped", "ec.Microsoft.AspNetCore.Http.Connections-1.connections-stopped")]
    [InlineData("Microsoft.AspNetCore.Http.Connections-1", "connections-timed-out-longer", "ec.Microsoft.AspNetCore.Http.Conne.connections-timed-out-longer")]
    [InlineData("Microsoft.AspNetCore.Http.Conn.Something", "connections-timed-out-longer", "ec.Microsoft.AspNetCore.Http.Conn.connections-timed-out-longer")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-very-very-long-event-name", "ec.very-very-very-very-very-very-very-very-very-long-event-name")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-very-long-event-name", "ec.Micr.very-very-very-very-very-very-very-very-long-event-name")]
    [InlineData("Microsoft.AspNetCore.One.Two", "very-very-very-very-very-very-very-long-event-name", "ec.Microsoft.very-very-very-very-very-very-very-long-event-name")]
    public async Task EventSourceNameShortening(string sourceName, string eventName, string expectedInstrumentName)
    {
        // Arrange
        List<Metric> metricItems = new();
        EventSource source = new(sourceName);
        IncrementingEventCounter connections = new(eventName, source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        connections.Increment(1);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        Metric metric = metricItems.Find(m => m.Name == expectedInstrumentName);
        Assert.NotNull(metric);
        Assert.Equal(1, GetActualValue(metric));
    }

    [Fact]
    public async Task InstrumentNameTooLong()
    {
        // Arrange
        List<Metric> metricItems = new();
        EventSource source = new("source");

        // ec.s. + event name is 63;
        string veryLongEventName = new string('e', 100);
        IncrementingEventCounter connections = new(veryLongEventName, source);

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddEventCountersInstrumentation(options =>
            {
                options.AddEventSources(source.Name);
            })
            .AddInMemoryExporter(metricItems)
            .Build();

        // Act
        connections.Increment(1);
        await Task.Delay(Delay);
        meterProvider.ForceFlush();

        // Assert
        foreach (var item in metricItems)
        {
            Assert.False(item.Name.StartsWith("ec.source.ee"));
            Assert.False(item.Name.StartsWith("ec.s.ee"));
        }
    }

    // polling and eventcounter with same instrument name?

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

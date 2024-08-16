// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Xunit;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests.Implementation;

[Collection("Redis")]
public class RedisProfilerEntryInstrumenterConfigurationTests
{
    private const int MaxTimeToAllowForFlush = 20000;

    private readonly ConnectionMultiplexer connection;
    private readonly RedisMetrics metrics;
    private readonly List<Metric> exportedItems = [];

    public RedisProfilerEntryInstrumenterConfigurationTests()
    {
        var connectionOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
        };
        connectionOptions.EndPoints.Add("localhost:6379");

        this.connection = ConnectionMultiplexer.Connect(connectionOptions);
        this.metrics = new RedisMetrics();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RedisProfilerEntryInstrumenter_WhenTracesAndMeters(bool enableMeter)
    {
        var activity = new Activity("redis-profiler");
        var profiledCommand = new TestProfiledCommand(DateTime.UtcNow);
        _ = this.CreateTraceProvider(true);
        var meterProvider = this.CreateMeterProvider(enableMeter);

        var result = RedisProfilerEntryInstrumenter.ProfilerCommandInstrument(activity, profiledCommand, this.metrics, new StackExchangeRedisInstrumentationOptions());

        Assert.NotNull(result);
        Assert.Equal("SET", result.DisplayName);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        if (enableMeter)
        {
            Assert.Single(this.exportedItems);
        }
        else
        {
            Assert.Empty(this.exportedItems);
        }

        return Task.CompletedTask;
    }

    private TracerProvider CreateTraceProvider(bool addInstrumentation)
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        if (addInstrumentation)
        {
            builder.AddRedisInstrumentation(this.connection);
        }

        return builder.Build()!;
    }

    private MeterProvider CreateMeterProvider(bool addInstrumentation)
    {
        var builder = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(this.exportedItems);
        if (addInstrumentation)
        {
            builder.AddRedisInstrumentation(this.connection);
        }

        return builder.Build()!;
    }
}

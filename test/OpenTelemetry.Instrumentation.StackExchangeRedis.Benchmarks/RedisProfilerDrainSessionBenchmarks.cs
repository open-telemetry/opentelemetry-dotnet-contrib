// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

[MemoryDiagnoser]
public class RedisProfilerDrainSessionBenchmarks
{
    private ActivityListener? activityListener;
    private Activity? parentActivity;
    private StackExchangeRedisInstrumentationOptions? options;
    private IProfiledCommand[]? sessionCommands;

    [Params(false, true)]
    public bool EnrichActivityWithTimingEvents { get; set; }

    [Params(1, 10, 100)]
    public int CommandCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.activityListener = new ActivityListener()
        {
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ShouldListenTo = (source) => source.Name == StackExchangeRedisConnectionInstrumentation.ActivitySourceName,
        };

        ActivitySource.AddActivityListener(this.activityListener);

        this.options = new StackExchangeRedisInstrumentationOptions()
        {
            EmitNewAttributes = true,
            EmitOldAttributes = false,
            EnrichActivityWithTimingEvents = this.EnrichActivityWithTimingEvents,
        };

        this.parentActivity = new Activity("redis-parent");
        this.parentActivity.SetIdFormat(ActivityIdFormat.W3C);
        this.parentActivity.Start();
        this.parentActivity.Stop();

        var commandCreated = DateTime.UtcNow;
        this.sessionCommands = new IProfiledCommand[this.CommandCount];

        for (var i = 0; i < this.CommandCount; i++)
        {
            this.sessionCommands[i] = BenchmarkProfiledCommand.Create(commandCreated.AddTicks(i), i);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => this.activityListener?.Dispose();

    [Benchmark]
    public void DrainSession() =>
        RedisProfilerEntryToActivityConverter.DrainSession(this.parentActivity, this.sessionCommands!, this.options!);
}

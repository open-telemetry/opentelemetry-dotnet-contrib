// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

[MemoryDiagnoser]
public class RedisProfilerEntryToActivityConverterBenchmarks
{
    private ActivityListener? activityListener;
    private Activity? parentActivity;
    private StackExchangeRedisInstrumentationOptions? options;
    private BenchmarkProfiledCommand? profiledCommand;

    [Params(false, true)]
    public bool EnrichActivityWithTimingEvents { get; set; }

    [Params(false, true)]
    public bool HasParentActivity { get; set; }

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

        this.profiledCommand = BenchmarkProfiledCommand.Create(DateTime.UtcNow, index: 0);

        if (this.HasParentActivity)
        {
            this.parentActivity = new Activity("redis-parent");
            this.parentActivity.SetIdFormat(ActivityIdFormat.W3C);
            this.parentActivity.Start();
            this.parentActivity.Stop();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => this.activityListener?.Dispose();

    [Benchmark]
    public Activity? ConvertCommand() =>
        RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(this.parentActivity, this.profiledCommand!, this.options!);
}

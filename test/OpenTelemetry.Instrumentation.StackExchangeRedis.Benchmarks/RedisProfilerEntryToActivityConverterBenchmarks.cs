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

    public enum SemanticConventionMode
    {
        Old,
        New,
        Both,
    }

    [Params(false, true)]
    public bool EnrichActivityWithTimingEvents { get; set; }

    [Params(false, true)]
    public bool HasParentActivity { get; set; }

    [Params(false, true)]
    public bool CommandFromEnum { get; set; }

    [Params(SemanticConventionMode.Old, SemanticConventionMode.New, SemanticConventionMode.Both)]
    public SemanticConventionMode Attributes { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var sourceName = StackExchangeRedisConnectionInstrumentation.ActivitySource.Name;

        this.activityListener = new ActivityListener()
        {
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ShouldListenTo = (source) => source.Name == sourceName,
        };

        ActivitySource.AddActivityListener(this.activityListener);

        this.options = new StackExchangeRedisInstrumentationOptions()
        {
            EmitNewAttributes = this.Attributes != SemanticConventionMode.Old,
            EmitOldAttributes = this.Attributes != SemanticConventionMode.New,
            EnrichActivityWithTimingEvents = this.EnrichActivityWithTimingEvents,
        };

        this.profiledCommand = BenchmarkProfiledCommand.Create(DateTime.UtcNow, index: 0, this.CommandFromEnum);

        if (this.HasParentActivity)
        {
            this.parentActivity = new Activity("redis-parent");
            this.parentActivity.SetIdFormat(ActivityIdFormat.W3C);
            this.parentActivity.Start();
            this.parentActivity.Stop();
        }

        if (this.ConvertCommand() == null)
        {
            throw new InvalidOperationException("The activity listener did not sample the instrumentation's activity.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => this.activityListener?.Dispose();

    [Benchmark]
    public Activity? ConvertCommand() =>
        RedisProfilerEntryToActivityConverter.ProfilerCommandToActivity(this.parentActivity, this.profiledCommand!, this.options!);
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.3593/23H2/2023Update/SunValley3)
Intel Core i9-10900K CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 8.0.300
  [Host]     : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2


| Method                                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
| ExportActivity                               | 266.2 ns | 0.34 ns | 0.28 ns |      - |         - |
| SerializeActivity                            | 243.1 ns | 1.42 ns | 1.26 ns |      - |         - |
| SerializeActivityWithoutTraceState           | 248.7 ns | 0.78 ns | 0.65 ns |      - |         - |
| SerializeActivityWithTraceState              | 273.8 ns | 3.75 ns | 3.32 ns |      - |         - |
| SerializeActivityWithTraceStateInGrandparent | 280.2 ns | 5.54 ns | 5.69 ns |      - |         - |
| CreateActivityWithGenevaExporter             | 452.1 ns | 4.50 ns | 3.99 ns | 0.0396 |     416 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

[MemoryDiagnoser]
public class TraceExporterBenchmarks
{
    private readonly Activity activity;
    private readonly Activity activityWithoutTraceState;
    private readonly Activity activityWithTraceState;
    private readonly Activity activityWithTraceStateInGrandparent;
    private readonly Batch<Activity> batch;
    private readonly MsgPackTraceExporter exporter;
    private readonly TracerProvider tracerProvider;
    private readonly ActivitySource activitySource = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark");

    public TraceExporterBenchmarks()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        this.batch = this.CreateBatch();

        #region Create activity to be used for Serialize and Export benchmark methods
        var activityListener = new ActivityListener
        {
            ActivityStarted = null,
            ActivityStopped = null,
            ShouldListenTo = (activitySource) => activitySource.Name == this.activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(activityListener);

        using (var testActivity = this.activitySource.StartActivity("Benchmark"))
        {
            this.activity = testActivity;
            this.activity.SetTag("tagString", "value");
            this.activity.SetTag("tagInt", 100);
            this.activity.SetStatus(Status.Error);
        }

        using (var grandparentActivity = this.activitySource.StartActivity("GrandparentActivity"))
        {
            using (var parentActivity = this.activitySource.StartActivity("ParentActivity", ActivityKind.Internal, grandparentActivity.Context))
            {
                using (var testActivity = this.activitySource.StartActivity("SayHello", ActivityKind.Internal, parentActivity.Context))
                {
                    // testActivity.TraceStateString = "some=state";
                    this.activityWithoutTraceState = testActivity;
                    this.activityWithoutTraceState.SetTag("tagString", "value");
                    this.activityWithoutTraceState.SetTag("tagInt", 100);
                    this.activityWithoutTraceState.SetStatus(Status.Error);
                }
            }
        }

        using (var grandparentActivity = this.activitySource.StartActivity("GrandparentActivity"))
        {
            using (var parentActivity = this.activitySource.StartActivity("ParentActivity", ActivityKind.Internal, grandparentActivity.Context))
            {
                using (var testActivity = this.activitySource.StartActivity("SayHello", ActivityKind.Internal, parentActivity.Context))
                {
                    testActivity.TraceStateString = "some=state";
                    this.activityWithTraceState = testActivity;
                    this.activityWithTraceState.SetTag("tagString", "value");
                    this.activityWithTraceState.SetTag("tagInt", 100);
                    this.activityWithTraceState.SetStatus(Status.Error);
                }
            }
        }

        using (var grandparentActivity = this.activitySource.StartActivity("GrandparentActivity"))
        {
            grandparentActivity.TraceStateString = "some=state";
            using (var parentActivity = this.activitySource.StartActivity("ParentActivity", ActivityKind.Internal, grandparentActivity.Context))
            {
                using (var testActivity = this.activitySource.StartActivity("SayHello", ActivityKind.Internal, parentActivity.Context))
                {
                    this.activityWithTraceStateInGrandparent = testActivity;
                    this.activityWithTraceStateInGrandparent.SetTag("tagString", "value");
                    this.activityWithTraceStateInGrandparent.SetTag("tagInt", 100);
                    this.activityWithTraceStateInGrandparent.SetStatus(Status.Error);
                }
            }
        }

        activityListener.Dispose();
        #endregion

        this.exporter = new MsgPackTraceExporter(new GenevaExporterOptions
        {
            ConnectionString = "EtwSession=OpenTelemetry",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.activitySource.Name)
            .AddGenevaTraceExporter(options =>
            {
                options.ConnectionString = "EtwSession=OpenTelemetry";
                options.PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };
            })
            .Build();
    }

    [Benchmark]
    public void ExportActivity()
    {
        this.exporter.Export(this.batch);
    }

    [Benchmark]
    public void SerializeActivity()
    {
        this.exporter.SerializeActivity(this.activity);
    }

    [Benchmark]
    public void SerializeActivityWithoutTraceState()
    {
        this.exporter.SerializeActivity(this.activityWithoutTraceState);
    }

    [Benchmark]
    public void SerializeActivityWithTraceState()
    {
        this.exporter.SerializeActivity(this.activityWithTraceState);
    }

    [Benchmark]
    public void SerializeActivityWithTraceStateInGrandparent()
    {
        this.exporter.SerializeActivity(this.activityWithTraceStateInGrandparent);
    }

    [Benchmark]
    public void CreateActivityWithGenevaExporter()
    {
        // this activity will be created and sent to Geneva exporter
        using var activity = this.activitySource.StartActivity("Benchmark");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.activity.Dispose();
        this.activitySource.Dispose();
        this.batch.Dispose();
        this.exporter.Dispose();
        this.tracerProvider.Dispose();
    }

    private Batch<Activity> CreateBatch()
    {
        using var batchGeneratorExporter = new BatchGeneratorExporter();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(this.activitySource.Name)
            .AddProcessor(new SimpleActivityExportProcessor(batchGeneratorExporter))
            .Build();

        using (var activity = this.activitySource.StartActivity("Benchmark"))
        {
            activity.SetTag("tagString", "value");
            activity.SetTag("tagInt", 100);
            activity.SetStatus(Status.Error);
        }

        return batchGeneratorExporter.Batch;
    }

    private class BatchGeneratorExporter : BaseExporter<Activity>
    {
        public Batch<Activity> Batch { get; set; }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            this.Batch = batch;
            return ExportResult.Success;
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.Geneva.TldExporter;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.3593/23H2/2023Update/SunValley3)
Intel Core i9-10900K CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 8.0.300
  [Host]     : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2


| Method                                           | Mean       | Error     | StdDev    | Allocated |
|------------------------------------------------- |-----------:|----------:|----------:|----------:|
| MsgPack_SerializeActivity                        | 239.467 ns | 1.1202 ns | 0.9930 ns |         - |
| TLD_SerializeActivity                            | 279.810 ns | 1.2504 ns | 0.9763 ns |         - |
| TLD_SerializeActivityWithoutTraceState           | 294.145 ns | 1.2952 ns | 1.1481 ns |         - |
| TLD_SerializeActivityWithTraceState              | 319.608 ns | 5.9573 ns | 5.5724 ns |         - |
| TLD_SerializeActivityWithTraceStateInGrandparent | 314.195 ns | 1.1738 ns | 0.9164 ns |         - |
| MsgPack_ExportActivity                           | 265.354 ns | 1.0098 ns | 0.9445 ns |         - |
| TLD_ExportActivity                               |   1.102 ns | 0.0469 ns | 0.0482 ns |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

[MemoryDiagnoser]
public class TLDTraceExporterBenchmarks
{
    private readonly Activity activity;
    private readonly Activity activityWithoutTraceState;
    private readonly Activity activityWithTraceState;
    private readonly Activity activityWithTraceStateInGrandparent;
    private readonly Batch<Activity> batch;
    private readonly MsgPackTraceExporter msgPackExporter;
    private readonly TldTraceExporter tldExporter;
    private readonly ActivitySource activitySource = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark");

    public TLDTraceExporterBenchmarks()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        this.batch = this.CreateBatch();

        using var activityListener = new ActivityListener
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
            this.activity?.SetTag("tagString", "value");
            this.activity?.SetTag("tagInt", 100);
            this.activity?.SetStatus(Status.Error);
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

        this.msgPackExporter = new MsgPackTraceExporter(new GenevaExporterOptions
        {
            ConnectionString = "EtwSession=OpenTelemetry",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });

        this.tldExporter = new TldTraceExporter(new GenevaExporterOptions()
        {
            ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableTraceLoggingDynamic=true",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });
    }

    [Benchmark]
    public void MsgPack_SerializeActivity()
    {
        this.msgPackExporter.SerializeActivity(this.activity);
    }

    [Benchmark]
    public void TLD_SerializeActivity()
    {
        this.tldExporter.SerializeActivity(this.activity);
    }

    [Benchmark]
    public void TLD_SerializeActivityWithoutTraceState()
    {
        this.tldExporter.SerializeActivity(this.activityWithoutTraceState);
    }

    [Benchmark]
    public void TLD_SerializeActivityWithTraceState()
    {
        this.tldExporter.SerializeActivity(this.activityWithTraceState);
    }

    [Benchmark]
    public void TLD_SerializeActivityWithTraceStateInGrandparent()
    {
        this.tldExporter.SerializeActivity(this.activityWithTraceStateInGrandparent);
    }

    [Benchmark]
    public void MsgPack_ExportActivity()
    {
        this.msgPackExporter.Export(this.batch);
    }

    [Benchmark]
    public void TLD_ExportActivity()
    {
        this.tldExporter.Export(this.batch);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.activity.Dispose();
        this.batch.Dispose();
        this.activitySource.Dispose();
        this.msgPackExporter.Dispose();
        this.tldExporter.Dispose();
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

// <copyright file="TraceExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22621
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT
  DefaultJob : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT


|            Method |     Mean |   Error |  StdDev |  Gen 0 | Allocated |
|------------------ |---------:|--------:|--------:|-------:|----------:|
|    ExportActivity | 719.9 ns | 5.83 ns | 5.45 ns | 0.0057 |      40 B |
| SerializeActivity | 361.7 ns | 1.09 ns | 0.97 ns | 0.0062 |      40 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class TraceExporterBenchmarks
{
    private readonly Activity activity;
    private readonly Batch<Activity> batch;
    private readonly MsgPackTraceExporter exporter;
    private readonly ActivitySource activitySource = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark");

    public TraceExporterBenchmarks()
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

    [GlobalCleanup]
    public void Cleanup()
    {
        this.activity.Dispose();
        this.activitySource.Dispose();
        this.batch.Dispose();
        this.exporter.Dispose();
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

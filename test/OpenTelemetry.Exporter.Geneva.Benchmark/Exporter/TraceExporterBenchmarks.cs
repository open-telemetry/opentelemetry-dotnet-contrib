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

using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.963)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2


|                           Method |     Mean |    Error |   StdDev |   Median |   Gen0 | Allocated |
|--------------------------------- |---------:|---------:|---------:|---------:|-------:|----------:|
|                   ExportActivity | 566.3 ns |  3.13 ns |  2.44 ns | 565.9 ns |      - |         - |
|                SerializeActivity | 313.3 ns |  1.71 ns |  1.60 ns | 313.0 ns |      - |         - |
| CreateActivityWithGenevaExporter | 940.5 ns | 18.77 ns | 54.14 ns | 911.4 ns | 0.0648 |     416 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class TraceExporterBenchmarks
{
    private readonly Activity activity;
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

// <copyright file="TLDTraceExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT


|                    Method |     Mean |   Error |  StdDev |  Gen 0 | Allocated |
|-------------------------- |---------:|--------:|--------:|-------:|----------:|
| MsgPack_SerializeActivity | 365.7 ns | 0.88 ns | 0.78 ns | 0.0062 |      40 B |
|     TLD_SerializeActivity | 471.3 ns | 3.03 ns | 2.69 ns | 0.0062 |      40 B |
|    MsgPack_ExportActivity | 726.6 ns | 6.20 ns | 5.50 ns | 0.0057 |      40 B |
|        TLD_ExportActivity | 831.6 ns | 6.38 ns | 5.97 ns | 0.0057 |      40 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class TLDTraceExporterBenchmarks
    {
        private readonly Activity activity;
        private readonly Batch<Activity> batch;
        private readonly GenevaTraceExporter msgPackExporter;
        private readonly TLDTraceExporter tldExporter;
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

            this.msgPackExporter = new GenevaTraceExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            });

            this.tldExporter = new TLDTraceExporter(new GenevaExporterOptions()
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
}

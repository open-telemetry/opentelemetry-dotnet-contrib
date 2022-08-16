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
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT

|            Method |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|------------------ |---------:|---------:|---------:|-------:|----------:|
|    ExportActivity | 687.2 ns | 13.73 ns | 20.55 ns | 0.0648 |     408 B |
| SerializeActivity | 392.3 ns |  4.87 ns |  4.32 ns | 0.0062 |      40 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    [MemoryDiagnoser]
    public class TraceExporterBenchmarks
    {
        private readonly Activity activity;
        private readonly GenevaTraceExporter exporter;
        private readonly ActivitySource sourceTestData = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark.TestData");
        private readonly ActivitySource activitySource = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark");
        private readonly TracerProvider tracerProvider;

        public TraceExporterBenchmarks()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == this.sourceTestData.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            });

            using (var tedious = this.sourceTestData.StartActivity("Benchmark"))
            {
                this.activity = tedious;
                this.activity?.SetTag("tagString", "value");
                this.activity?.SetTag("tagInt", 100);
                this.activity?.SetStatus(Status.Error);
            }

            this.exporter = new GenevaTraceExporter(new GenevaExporterOptions
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
            // this activity will be created and feed into the actual Geneva exporter
            using var activity = this.activitySource.StartActivity("Benchmark");
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
            this.sourceTestData.Dispose();
            this.activitySource.Dispose();
            this.exporter.Dispose();
            this.tracerProvider.Dispose();
        }
    }
}

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
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.101
  [Host]     : .NET 5.0.12 (5.0.1221.52207), X64 RyuJIT
  DefaultJob : .NET 5.0.12 (5.0.1221.52207), X64 RyuJIT

|                    Method |      Mean |    Error |   StdDev |  Gen 0 | Allocated |
|-------------------------- |----------:|---------:|---------:|-------:|----------:|
|      CreateBoringActivity |  16.05 ns | 0.053 ns | 0.049 ns |      - |         - |
|     CreateTediousActivity | 509.26 ns | 2.105 ns | 1.969 ns | 0.0486 |     408 B |
| CreateInterestingActivity | 959.87 ns | 6.014 ns | 5.625 ns | 0.0477 |     408 B |
|         SerializeActivity | 506.55 ns | 3.862 ns | 3.612 ns | 0.0095 |      80 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    [MemoryDiagnoser]
    public class TraceExporterBenchmarks
    {
        private readonly Random r = new Random();
        private readonly Activity activity;
        private readonly GenevaTraceExporter exporter;
        private readonly ActivitySource sourceBoring = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark.Boring");
        private readonly ActivitySource sourceTedious = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark.Tedious");
        private readonly ActivitySource sourceInteresting = new ActivitySource("OpenTelemetry.Exporter.Geneva.Benchmark.Interesting");

        public TraceExporterBenchmarks()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;

            ActivitySource.AddActivityListener(new ActivityListener
            {
                ActivityStarted = null,
                ActivityStopped = null,
                ShouldListenTo = (activitySource) => activitySource.Name == this.sourceTedious.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            });

            using (var tedious = this.sourceTedious.StartActivity("Benchmark"))
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

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource(this.sourceInteresting.Name)
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
        public void CreateBoringActivity()
        {
            // this activity won't be created as there is no listener
            using var activity = this.sourceBoring.StartActivity("Benchmark");
        }

        [Benchmark]
        public void CreateTediousActivity()
        {
            // this activity will be created and feed into an ActivityListener that simply drops everything on the floor
            using var activity = this.sourceTedious.StartActivity("Benchmark");
        }

        [Benchmark]
        public void CreateInterestingActivity()
        {
            // this activity will be created and feed into the actual Geneva exporter
            using var activity = this.sourceInteresting.StartActivity("Benchmark");
        }

        [Benchmark]
        public void SerializeActivity()
        {
            this.exporter.SerializeActivity(this.activity);
        }
    }
}

// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace OpenTelemetry.Exporter.OneCollector.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (Debugger.IsAttached)
        {
            // Note: This block is so you can start the project with debugger
            // attached and step through the code. It is helpful when working on
            // it.
            var benchmarks = new LogRecordCommonSchemaJsonHttpPostBenchmarks
            {
                NumberOfBatches = 10_000,
                NumberOfLogRecordsPerBatch = 1000,
                EnableCompression = true,
            };

            benchmarks.GlobalSetup();

            benchmarks.Export();

            benchmarks.GlobalCleanup();
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}

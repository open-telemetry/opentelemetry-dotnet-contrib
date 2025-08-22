// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using JetBrains.Profiler.Api;
using OpenTelemetry.Instrumentation.Benchmarks;

if (Debugger.IsAttached || (args.Length > 0 && args[0] == "execute"))
{
    var benchmarks = new SqlProcessorBenchmarks
    {
        Sql = "SELECT * FROM Orders o, OrderDetails od",
        Iterations = 100,
    };

    MemoryProfiler.CollectAllocations(true);

    MemoryProfiler.GetSnapshot();

    benchmarks.Simple();

    MemoryProfiler.GetSnapshot();
}
else
{
    var config = ManualConfig.Create(DefaultConfig.Instance)
        .WithArtifactsPath(@"..\..\..\BenchmarkResults");

    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
}

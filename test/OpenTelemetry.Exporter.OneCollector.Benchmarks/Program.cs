// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Running;
using OpenTelemetry.Exporter.OneCollector.Benchmarks;

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

    return 0;
}
else
{
    var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    return summaries.SelectMany(p => p.Reports).Any((p) => !p.Success) ? 1 : 0;
}

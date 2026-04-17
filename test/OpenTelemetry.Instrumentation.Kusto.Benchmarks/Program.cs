// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Running;
using OpenTelemetry.Instrumentation.Kusto.Benchmarks;

if (Debugger.IsAttached)
{
    var benchmarks = new InstrumentationBenchmarks();
    benchmarks.Setup();
    benchmarks.FailedQuery();
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(InstrumentationBenchmarks).Assembly).Run(args);
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace OpenTelemetry.Instrumentation.Kusto.Benchmarks;

internal class Program
{
    private static void Main(string[] args)
    {
        if (Debugger.IsAttached)
        {
            var benchmarks = new InstrumentationBenchmarks();
            benchmarks.Setup();
            benchmarks.FailedQuery();
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}

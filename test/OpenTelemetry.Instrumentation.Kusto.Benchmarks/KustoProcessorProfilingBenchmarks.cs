// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.VSDiagnostics;
using OpenTelemetry.Instrumentation.Kusto.Implementation;

namespace OpenTelemetry.Instrumentation.Kusto.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
[DotNetObjectAllocDiagnoser]
[DotNetObjectAllocJobConfiguration]
public class KustoProcessorProfilingBenchmarks
{
    public string Query { get; } = "StormEvents | join kind=inner (PopulationData) on State | project State, EventType, Population";

    [Benchmark]
    public object ProcessSummarizeAndSanitize() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, this.Query);
}

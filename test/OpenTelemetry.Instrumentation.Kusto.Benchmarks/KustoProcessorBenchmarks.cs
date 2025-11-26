// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using OpenTelemetry.Instrumentation.Kusto.Implementation;

namespace OpenTelemetry.Instrumentation.Kusto.Benchmarks;

[MemoryDiagnoser]
public class KustoProcessorBenchmarks
{
    public string Query { get; set; } = "StormEvents | join kind=inner (PopulationData) on State | project State, EventType, Population";

    [Benchmark]
    public void ProcessSummarizeAndSanitize() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, this.Query);

    [Benchmark]
    public void ProcessSummarizeOnly() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: false, this.Query);

    [Benchmark]
    public void ProcessSanitizeOnly() => KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, this.Query);

    [Benchmark]
    public void ProcessNeither() => KustoProcessor.Process(shouldSummarize: false, shouldSanitize: false, this.Query);
}

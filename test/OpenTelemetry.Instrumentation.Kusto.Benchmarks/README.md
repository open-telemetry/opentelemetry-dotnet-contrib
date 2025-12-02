# OpenTelemetry.Instrumentation.Kusto.Benchmarks

This project contains benchmarks for the OpenTelemetry Kusto instrumentation library.

## Running the Benchmarks

To run all benchmarks:

```bash
dotnet run --configuration Release --framework net10.0 --project test\OpenTelemetry.Instrumentation.Kusto.Benchmarks
```

Then choose the benchmark class that you want to run by entering the required
option number from the list of options shown on the Console window.

> [!TIP]
> The Profiling benchmarks are designed to run quickly and use the Visual Studio diagnosers to gather performance data.

## Results

### Full instrumentation

```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7092/24H2/2024Update/HudsonValley)
Intel Core i9-10940X CPU 3.30GHz (Max: 3.31GHz), 1 CPU, 28 logical and 14 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4


```
| Method             | Mean         | Error      | StdDev     | Gen0   | Gen1   | Allocated |
|------------------- |-------------:|-----------:|-----------:|-------:|-------:|----------:|
| SuccessfulQuery    | 12,866.04 ns | 252.865 ns | 378.477 ns | 1.1597 | 0.0153 |   11792 B |
| FailedQuery        | 13,736.88 ns | 271.636 ns | 453.842 ns | 1.1902 | 0.0153 |   11984 B |
| TraceListenerOnly  | 13,281.18 ns | 261.834 ns | 311.695 ns | 1.1444 | 0.0153 |   11592 B |
| MetricListenerOnly |     88.40 ns |   1.783 ns |   2.318 ns | 0.0095 |      - |      96 B |

### Summarization and sanitization processing

Summarization and sanitization are the most expensive parts of instrumentation, so there are benchmarks to measure their
specific cost.

```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.7093)
Intel Core Ultra 7 165H 3.80GHz, 1 CPU, 22 logical and 16 physical cores
.NET SDK 10.0.100-rc.2.25502.107
  [Host]     : .NET 10.0.0 (10.0.0-rc.2.25502.107, 10.0.25.50307), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.0 (10.0.0-rc.2.25502.107, 10.0.25.50307), X64 RyuJIT x86-64-v3


```
| Method                      | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------------------------- |----------:|----------:|----------:|-------:|-------:|----------:|
| ProcessSummarizeAndSanitize | 10.141 μs | 0.1926 μs | 0.2436 μs | 1.0834 | 0.0153 |  13.36 KB |
| ProcessSummarizeOnly        |  9.549 μs | 0.1772 μs | 0.1571 μs | 1.0071 | 0.0153 |  12.48 KB |
| ProcessSanitizeOnly         |  4.154 μs | 0.0827 μs | 0.1832 μs | 0.5798 | 0.0038 |   7.13 KB |
| ProcessNeither              | 0.0566 ns | 0.0259 ns | 0.0610 ns |      - |      - |         - |

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

// TODO: Includes private fixes in Kusto-Query-Langugage

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

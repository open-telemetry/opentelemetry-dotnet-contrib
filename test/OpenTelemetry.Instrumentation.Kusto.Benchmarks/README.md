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

```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7092/24H2/2024Update/HudsonValley)
Intel Core i9-10940X CPU 3.30GHz (Max: 3.31GHz), 1 CPU, 28 logical and 14 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4


```
| Method                      | Mean     | Error    | StdDev   | Median   | Gen0   | Gen1   | Allocated |
|---------------------------- |---------:|---------:|---------:|---------:|-------:|-------:|----------:|
| ProcessSummarizeAndSanitize | 31.85 μs | 0.941 μs | 2.686 μs | 30.64 μs | 4.8828 | 0.0610 |  48.38 KB |
| ProcessSummarizeOnly        | 26.73 μs | 0.531 μs | 1.311 μs | 26.47 μs | 4.7607 | 0.4272 |  47.25 KB |
| ProcessSanitizeOnly         | 25.91 μs | 0.509 μs | 0.778 μs | 25.75 μs | 4.8523 |      - |  47.74 KB |

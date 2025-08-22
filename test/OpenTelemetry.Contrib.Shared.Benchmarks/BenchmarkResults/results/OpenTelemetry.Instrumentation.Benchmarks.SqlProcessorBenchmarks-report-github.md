```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26100.4946)
Unknown processor
.NET SDK 9.0.304
  [Host]     : .NET 9.0.8 (9.0.825.36511), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 9.0.8 (9.0.825.36511), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method | Sql                  | Iterations | Mean        | Error     | StdDev    | Allocated |
|------- |--------------------- |----------- |------------:|----------:|----------:|----------:|
| **Simple** | **SELEC(...)ls od [39]** | **1**          |    **31.47 ns** |  **0.657 ns** |  **1.523 ns** |         **-** |
| **Simple** | **SELEC(...)ls od [39]** | **20**         |   **602.11 ns** | **11.166 ns** | **14.520 ns** |         **-** |
| **Simple** | **SELE(...)_id) [101]**  | **1**          |    **67.78 ns** |  **0.840 ns** |  **0.785 ns** |         **-** |
| **Simple** | **SELE(...)_id) [101]**  | **20**         | **1,364.29 ns** | **16.387 ns** | **14.526 ns** |         **-** |

```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26100.4946)
Unknown processor
.NET SDK 9.0.304
  [Host]     : .NET 9.0.8 (9.0.825.36511), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 9.0.8 (9.0.825.36511), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method | Sql                  | Iterations | Mean      | Error     | StdDev    | Allocated |
|------- |--------------------- |----------- |----------:|----------:|----------:|----------:|
| **Simple** | **SELEC(...)ls od [39]** | **1**          |  **16.42 ns** |  **0.362 ns** |  **0.818 ns** |         **-** |
| **Simple** | **SELEC(...)ls od [39]** | **20**         | **335.73 ns** |  **6.616 ns** |  **9.698 ns** |         **-** |
| **Simple** | **SELE(...)_id) [101]**  | **1**          |  **33.42 ns** |  **0.700 ns** |  **1.445 ns** |         **-** |
| **Simple** | **SELE(...)_id) [101]**  | **20**         | **654.29 ns** | **12.859 ns** | **13.759 ns** |         **-** |

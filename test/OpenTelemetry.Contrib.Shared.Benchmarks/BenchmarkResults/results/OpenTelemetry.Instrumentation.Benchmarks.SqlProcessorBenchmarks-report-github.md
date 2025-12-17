```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7462/25H2/2025Update/HudsonValley2)
Intel Core Ultra 9 185H 2.50GHz, 1 CPU, 22 logical and 16 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3


```
| Method          | Sql                  | Mean     | Error   | StdDev  | Allocated |
|---------------- |--------------------- |---------:|--------:|--------:|----------:|
| **GetSanitizedSql** | **CREAT(...)s(Id) [56]** | **229.2 ns** | **1.90 ns** | **1.78 ns** |     **208 B** |
| **GetSanitizedSql** | **DELET(...) = 42 [32]** | **156.0 ns** | **0.84 ns** | **0.71 ns** |     **128 B** |
| **GetSanitizedSql** | **INSER(...)3e-5) [76]** | **269.6 ns** | **1.94 ns** | **1.72 ns** |     **192 B** |
| **GetSanitizedSql** | **SELEC(...)ls od [39]** | **172.7 ns** | **3.40 ns** | **4.54 ns** |     **184 B** |
| **GetSanitizedSql** | **SELE(...)tory [111]**  | **261.3 ns** | **4.58 ns** | **5.63 ns** |     **424 B** |
| **GetSanitizedSql** | **SELEC(...)table [69]** | **121.7 ns** | **2.43 ns** | **3.32 ns** |     **128 B** |
| **GetSanitizedSql** | **SELEC(...) c.Id [74]** | **247.3 ns** | **3.98 ns** | **3.73 ns** |     **248 B** |
| **GetSanitizedSql** | **SELE(...)_id) [101]**  | **292.2 ns** | **3.28 ns** | **2.90 ns** |     **312 B** |
| **GetSanitizedSql** | **UPDAT(...) = 42 [44]** | **178.9 ns** | **2.44 ns** | **2.28 ns** |     **144 B** |

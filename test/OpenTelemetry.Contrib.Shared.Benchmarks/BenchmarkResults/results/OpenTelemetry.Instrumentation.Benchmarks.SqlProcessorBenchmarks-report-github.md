```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
Unknown processor
.NET SDK 9.0.304
  [Host]     : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2


```
| Method          | Sql                  | Mean     | Error   | StdDev  | Allocated |
|---------------- |--------------------- |---------:|--------:|--------:|----------:|
| **GetSanitizedSql** | **CREAT(...)s(Id) [56]** | **237.5 ns** | **3.17 ns** | **2.97 ns** |     **248 B** |
| **GetSanitizedSql** | **DELET(...) = 42 [32]** | **177.5 ns** | **3.34 ns** | **3.57 ns** |     **128 B** |
| **GetSanitizedSql** | **INSER(...)3e-5) [76]** | **272.8 ns** | **3.13 ns** | **2.62 ns** |     **192 B** |
| **GetSanitizedSql** | **SELEC(...)ls od [39]** | **160.5 ns** | **2.06 ns** | **1.83 ns** |     **184 B** |
| **GetSanitizedSql** | **SELE(...)tory [111]**  | **276.9 ns** | **2.23 ns** | **1.98 ns** |     **424 B** |
| **GetSanitizedSql** | **SELEC(...)table [69]** | **114.1 ns** | **1.51 ns** | **1.41 ns** |     **128 B** |
| **GetSanitizedSql** | **SELEC(...) c.Id [74]** | **255.5 ns** | **1.71 ns** | **1.52 ns** |     **264 B** |
| **GetSanitizedSql** | **SELE(...)_id) [101]**  | **303.3 ns** | **2.41 ns** | **2.25 ns** |     **312 B** |
| **GetSanitizedSql** | **UPDAT(...) = 42 [44]** | **199.8 ns** | **2.29 ns** | **2.14 ns** |     **144 B** |

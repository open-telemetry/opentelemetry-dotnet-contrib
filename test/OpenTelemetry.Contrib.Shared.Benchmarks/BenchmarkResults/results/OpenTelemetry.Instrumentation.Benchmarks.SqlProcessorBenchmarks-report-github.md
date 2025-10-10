```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
Unknown processor
.NET SDK 9.0.305
  [Host]     : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2


```
| Method          | Sql                  | Mean     | Error   | StdDev  | Allocated |
|---------------- |--------------------- |---------:|--------:|--------:|----------:|
| **GetSanitizedSql** | **CREAT(...)s(Id) [56]** | **214.9 ns** | **4.30 ns** | **8.18 ns** |     **248 B** |
| **GetSanitizedSql** | **DELET(...) = 42 [32]** | **177.8 ns** | **3.53 ns** | **3.47 ns** |     **128 B** |
| **GetSanitizedSql** | **INSER(...)3e-5) [76]** | **263.4 ns** | **4.11 ns** | **3.85 ns** |     **192 B** |
| **GetSanitizedSql** | **SELEC(...)ls od [39]** | **153.8 ns** | **1.68 ns** | **1.57 ns** |     **184 B** |
| **GetSanitizedSql** | **SELE(...)tory [111]**  | **252.8 ns** | **4.04 ns** | **4.33 ns** |     **424 B** |
| **GetSanitizedSql** | **SELEC(...)table [69]** | **109.7 ns** | **1.29 ns** | **1.14 ns** |     **128 B** |
| **GetSanitizedSql** | **SELEC(...) c.Id [74]** | **246.8 ns** | **4.52 ns** | **4.23 ns** |     **264 B** |
| **GetSanitizedSql** | **SELE(...)_id) [101]**  | **291.8 ns** | **5.12 ns** | **4.79 ns** |     **312 B** |
| **GetSanitizedSql** | **UPDAT(...) = 42 [44]** | **189.6 ns** | **3.69 ns** | **5.05 ns** |     **144 B** |

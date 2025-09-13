```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
Unknown processor
.NET SDK 9.0.304
  [Host]     : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.9 (9.0.925.41916), X64 RyuJIT AVX2


```
| Method          | Sql                  | Mean     | Error    | StdDev    | Median   | Allocated |
|---------------- |--------------------- |---------:|---------:|----------:|---------:|----------:|
| **GetSanitizedSql** | **CREAT(...)s(Id) [56]** | **420.6 ns** | **22.18 ns** |  **60.71 ns** | **428.3 ns** |     **584 B** |
| **GetSanitizedSql** | **DELET(...) = 42 [32]** | **255.7 ns** |  **7.11 ns** |  **20.18 ns** | **252.0 ns** |     **448 B** |
| **GetSanitizedSql** | **INSER(...)3e-5) [76]** | **514.4 ns** | **16.39 ns** |  **48.33 ns** | **513.3 ns** |     **680 B** |
| **GetSanitizedSql** | **SELEC(...)ls od [39]** | **439.8 ns** | **39.56 ns** | **116.65 ns** | **374.5 ns** |     **528 B** |
| **GetSanitizedSql** | **SELE(...)tory [111]**  | **584.5 ns** | **11.55 ns** |  **24.36 ns** | **583.5 ns** |    **1056 B** |
| **GetSanitizedSql** | **SELEC(...)table [69]** | **269.9 ns** |  **8.34 ns** |  **23.79 ns** | **271.8 ns** |     **600 B** |
| **GetSanitizedSql** | **SELEC(...) c.Id [74]** | **563.1 ns** | **22.71 ns** |  **66.25 ns** | **553.4 ns** |     **736 B** |
| **GetSanitizedSql** | **SELE(...)_id) [101]**  | **691.8 ns** | **19.53 ns** |  **56.02 ns** | **693.5 ns** |     **912 B** |
| **GetSanitizedSql** | **UPDAT(...) = 42 [44]** | **328.1 ns** | **11.58 ns** |  **33.40 ns** | **329.8 ns** |     **504 B** |

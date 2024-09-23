```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method       | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------- |---------:|---------:|---------:|-------:|----------:|
| UncachedLoad | 24.03 μs | 0.227 μs | 0.213 μs | 4.0283 |  16.31 KB |
| CachedLoad   | 41.16 μs | 0.238 μs | 0.223 μs | 6.7139 |  27.23 KB |

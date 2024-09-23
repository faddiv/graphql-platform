```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method       | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------- |-----------:|---------:|---------:|-------:|----------:|
| UncachedLoad | 6,257.7 ns | 96.24 ns | 90.02 ns | 0.3052 |    1280 B |
| CachedLoad   |   103.5 ns |  1.57 ns |  1.39 ns |      - |         - |

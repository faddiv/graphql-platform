```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method       | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------- |----------:|----------:|----------:|-------:|----------:|
| UncachedLoad | 25.802 μs | 0.5110 μs | 0.4780 μs | 4.0894 |  16.34 KB |
| CachedLoad   |  6.443 μs | 0.0819 μs | 0.0684 μs | 0.3510 |   1.45 KB |

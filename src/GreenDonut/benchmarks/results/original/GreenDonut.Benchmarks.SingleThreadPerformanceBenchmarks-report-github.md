```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method       | Mean       | Error     | StdDev    | Gen0   | Allocated |
|------------- |-----------:|----------:|----------:|-------:|----------:|
| UncachedLoad | 6,206.6 ns | 121.99 ns | 178.81 ns | 0.2899 |    1256 B |
| CachedLoad   |   837.6 ns |   3.94 ns |   3.68 ns | 0.1278 |     528 B |

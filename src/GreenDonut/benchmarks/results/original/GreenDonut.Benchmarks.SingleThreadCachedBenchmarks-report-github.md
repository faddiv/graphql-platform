```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                    | Mean        | Error     | StdDev    | Gen0   | Allocated |
|-------------------------- |------------:|----------:|----------:|-------:|----------:|
| SingleThreadCached        |    808.9 ns |   6.82 ns |   6.04 ns | 0.1268 |     528 B |
| SingleThreadFirstHitCache | 10,181.3 ns | 141.12 ns | 125.10 ns | 0.6409 |    2728 B |

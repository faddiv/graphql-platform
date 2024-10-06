```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                    | Mean        | Error     | StdDev    | Gen0   | Allocated |
|-------------------------- |------------:|----------:|----------:|-------:|----------:|
| SingleThreadCached        |    101.3 ns |   0.46 ns |   0.43 ns |      - |         - |
| SingleThreadFirstHitCache | 11,328.1 ns | 226.52 ns | 222.47 ns | 0.5798 |    2480 B |

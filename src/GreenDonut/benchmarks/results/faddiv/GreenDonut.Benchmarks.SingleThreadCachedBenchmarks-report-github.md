```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4317/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.403
  [Host]     : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2


```
| Method                    | Mean        | Error      | StdDev     | Gen0   | Allocated |
|-------------------------- |------------:|-----------:|-----------:|-------:|----------:|
| SingleThreadCached        |    92.27 ns |   0.376 ns |   0.333 ns |      - |         - |
| SingleThreadFirstHitCache | 8,507.41 ns | 151.790 ns | 141.984 ns | 0.4578 |    1976 B |

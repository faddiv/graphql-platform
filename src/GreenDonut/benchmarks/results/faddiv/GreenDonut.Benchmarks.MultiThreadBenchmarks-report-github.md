```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4317/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.403
  [Host]     : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|----------:|
| MultiThreadCachedLoad |   5.533 μs | 0.0250 μs | 0.0222 μs |  0.2365 |    1008 B |
| MultiThreadFirstHit   | 190.457 μs | 0.6971 μs | 0.6179 μs | 14.1602 |   57765 B |
```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|-------:|----------:|
| MultiThreadCachedLoad |   6.635 μs | 0.0307 μs | 0.0272 μs |  0.7172 |      - |   2.94 KB |
| MultiThreadFirstHit   | 194.671 μs | 1.0485 μs | 0.9808 μs | 17.5781 | 0.2441 |   69.4 KB |

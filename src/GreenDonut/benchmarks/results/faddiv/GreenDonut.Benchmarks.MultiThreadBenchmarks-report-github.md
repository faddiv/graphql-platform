```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|-------:|----------:|
| MultiThreadCachedLoad |   6.187 μs | 0.0259 μs | 0.0243 μs |  0.2365 |      - |    1008 B |
| MultiThreadFirstHit   | 190.706 μs | 1.3017 μs | 1.1540 μs | 17.5781 | 0.4883 |   70822 B |

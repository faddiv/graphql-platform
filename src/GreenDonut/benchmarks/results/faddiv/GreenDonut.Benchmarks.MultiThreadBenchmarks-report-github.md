```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|-------:|----------:|
| MultiThreadCachedLoad |   6.337 μs | 0.0244 μs | 0.0228 μs |  0.2365 |      - |    1008 B |
| MultiThreadFirstHit   | 191.700 μs | 0.8927 μs | 0.7914 μs | 16.6016 | 1.4648 |   66878 B |

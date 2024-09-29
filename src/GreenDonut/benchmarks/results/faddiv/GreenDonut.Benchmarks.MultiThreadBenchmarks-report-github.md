```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|----------:|
| MultiThreadCachedLoad |   6.467 μs | 0.0562 μs | 0.0498 μs |  0.2365 |    1008 B |
| MultiThreadFirstHit   | 179.194 μs | 2.2946 μs | 1.9161 μs | 17.0898 |   68564 B |

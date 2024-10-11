```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4317/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.403
  [Host]     : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2


```
| Method                | Mean       | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|---------------------- |-----------:|----------:|----------:|--------:|-------:|----------:|
| MultiThreadCachedLoad |   5.596 μs | 0.0308 μs | 0.0288 μs |  0.2365 |      - |    1008 B |
| MultiThreadFirstHit   | 223.183 μs | 0.8600 μs | 0.7624 μs | 17.0898 | 0.4883 |   70669 B |

```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


```
| Method                | Mean      | Error    | StdDev   | Gen0    | Allocated |
|---------------------- |----------:|---------:|---------:|--------:|----------:|
| MultiThreadCachedLoad |  39.71 μs | 0.176 μs | 0.156 μs |  6.5918 |  26.82 KB |
| MultiThreadFirstHit   | 161.48 μs | 0.710 μs | 0.629 μs | 17.5781 |   70.9 KB |

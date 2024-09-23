```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  Job-DOTDWR : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

```
| Method              | Mean     | Error   | StdDev   | Allocated |
|-------------------- |---------:|--------:|---------:|----------:|
| CachedLoadWithReset | 107.1 μs | 7.23 μs | 20.97 μs |   3.16 KB |

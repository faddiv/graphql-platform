```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4169/23H2/2023Update/SunValley3)
AMD Ryzen 5 2600, 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.400
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  Job-LLRFQJ : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

```
| Method              | Mean     | Error    | StdDev   | Allocated |
|-------------------- |---------:|---------:|---------:|----------:|
| CachedLoadWithReset | 94.04 μs | 8.339 μs | 24.46 μs |   2.99 KB |

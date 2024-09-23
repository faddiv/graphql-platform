// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

Console.WriteLine("Hello, World!");
/*
var v = new MultiThreadPerformanceBenchmarks();
v.Setup();
var result = await v.UncachedLoad();
var result2 = await v.CachedLoad();
*/

BenchmarkRunner.Run(
    [
        typeof(SingleThreadPerformanceBenchmarks),
        typeof(MultiThreadPerformanceBenchmarks),
        typeof(CachedBenchmarks)
    ], args: args);

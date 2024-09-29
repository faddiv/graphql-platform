// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

BenchmarkRunner.Run(
    [
        typeof(SingleThreadUncachedBenchmarks),
        typeof(SingleThreadCachedBenchmarks),
        typeof(MultiThreadBenchmarks)
    ], args: args);

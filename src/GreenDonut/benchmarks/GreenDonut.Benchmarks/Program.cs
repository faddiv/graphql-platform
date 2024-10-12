// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

Console.WriteLine("Hello, World!");

await SubscriptionBenchmarks.Test();

BenchmarkRunner.Run(
    [
        typeof(SingleThreadUncachedBenchmarks),
        typeof(SingleThreadCachedBenchmarks),
        typeof(MultiThreadBenchmarks),
        typeof(SubscriptionBenchmarks),
        typeof(MultiThreadV2Benchmarks),
    ], args: args);

// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

await MultiThreadV2Benchmarks.Test();
await SubscriptionBenchmarks.Test();

BenchmarkRunner.Run(
    [
        typeof(SingleThreadUncachedBenchmarks),
        typeof(SingleThreadCachedBenchmarks),
        typeof(MultiThreadBenchmarks),
        typeof(SubscriptionBenchmarks),
        typeof(MultiThreadV2Benchmarks),
    ], args: args);

// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

await SubscriptionBenchmarks.Test();

BenchmarkRunner.Run(
    [
        //typeof(SingleThreadUncachedBenchmarks),
        //typeof(SingleThreadCachedBenchmarks),
        //typeof(MultiThreadBenchmarks),
        typeof(SubscriptionBenchmarks)
    ], args: args);

// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using GreenDonut.Benchmarks;

await MultiThreadBenchmarks.Test();
await SubscriptionBenchmarks.Test();
await SingleThreadBenchmarks.Test();

BenchmarkRunner.Run(
    [
        typeof(SingleThreadBenchmarks),
        typeof(MultiThreadBenchmarks),
        typeof(SubscriptionBenchmarks),
    ], args: args);

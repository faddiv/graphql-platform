using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using GreenDonut.LoadTests.TestedImplementations;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class MultiThreadBenchmarks
{
    private IServiceScope _customScope = null!;
    private ServiceProvider _sp = null!;

    // ReSharper disable once MemberCanBePrivate.Global
    [Params(Defaults.Original, Defaults.VNext)]
    public string Version { get; set; } = "Original";

    [GlobalSetup]
    public void Setup()
    {
        _sp = Tests.CreateServiceProvider();

        _customScope = _sp.CreateScope();
    }

    [Benchmark]
    public Task<Result> MultiThreadCachedLoad()
    {
        return Tests.ExecuteTestWith(_customScope, Version, default);
    }

    [Benchmark]
    public Task<Result> MultiThreadFirstHit()
    {
        return Tests.ExecuteTestWith(_sp, Version, default);
    }

    internal static async Task Test()
    {
        var b = new MultiThreadBenchmarks();
        b.Setup();

        b.Version = "Original";
        await TestMultiThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);

        b.Version = "vNext";
        await TestMultiThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);
    }

    private static async Task TestMultiThreadCachedLoad(MultiThreadBenchmarks b)
    {
        var result = await b.MultiThreadCachedLoad();
        Asserts.Assert(result == Result.Ok, version: b.Version, actual: result);
        var result2 = await b.MultiThreadCachedLoad();
        Asserts.Assert(result2 == Result.Ok, version: b.Version, actual: result2);
    }

    private static async Task TestMultiThreadFirstHit(MultiThreadBenchmarks b)
    {
        var result = await b.MultiThreadFirstHit();
        Asserts.Assert(result == Result.Ok, version: b.Version, actual: result);
    }
}

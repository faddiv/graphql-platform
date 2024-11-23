using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using GreenDonut.ExampleDataLoader;
using GreenDonut.ExampleDataLoader.TestClasses;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadBenchmarks
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
        var dataLoaderCached1 = Tests.ProvideDataLoader(_customScope, Defaults.Original);
        var task1 = dataLoaderCached1.LoadAsync("abc2");
        var dataLoaderCached2 = Tests.ProvideDataLoader(_customScope, Defaults.VNext);
        var task2 = dataLoaderCached2.LoadAsync("abc2");
        Tests.GetManualBatchScheduler(_customScope).Dispatch();
        task1.Wait();
        task2.Wait();
    }

    [Benchmark]
    public Task<string?> SingleThreadCached()
    {
        var dataLoaderCached = Tests.ProvideDataLoader(_customScope, Version);
        return dataLoaderCached.LoadAsync("abc2");
    }

    [Benchmark]
    public async Task<string?> SingleThreadFirstHit()
    {
        using var sc = _sp.CreateScope();
        var dataLoader = Tests.ProvideDataLoader(sc, Version);
        var loadAsync = dataLoader.LoadAsync("abc2");
        await Tests.GetManualBatchScheduler(sc).DispatchAsync();
        return await loadAsync;
    }

    internal static async Task Test()
    {
        var b = new SingleThreadBenchmarks();
        b.Setup();

        b.Version = "Original";
        await TestSingleThreadFirstHit(b);
        await TestSingleThreadCached(b);

        b.Version = "vNext";
        await TestSingleThreadFirstHit(b);
        await TestSingleThreadCached(b);
    }

    private static async Task TestSingleThreadFirstHit(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadFirstHit();
        Asserts.Assert(result == "Value:abc2", b.Version, actual: result);
    }

    private static async Task TestSingleThreadCached(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadCached();
        Asserts.Assert(result == "Value:abc2", b.Version, actual: result);
    }
}

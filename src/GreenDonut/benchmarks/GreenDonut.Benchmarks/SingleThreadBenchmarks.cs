using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadBenchmarks
{
    private IServiceScope _customScope = null!;
    private ServiceProvider _sp = null!;

    // ReSharper disable once MemberCanBePrivate.Global
    [Params("Original", "vNext")]
    public string Version { get; set; } = "Original";

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.TryAddDataLoader2Core();
        services.AddDataLoader<CustomBatchDataLoader>();
        services.AddDataLoader<CustomBatchDataLoader2>();

        _sp = services.BuildServiceProvider();

        _customScope = _sp.CreateScope();
    }

    [Benchmark]
    public Task<string?> SingleThreadCached()
    {
        var dataLoaderCached = ProvideDataLoader(_customScope);
        return dataLoaderCached.LoadAsync("abc2");
    }

    [Benchmark]
    public async Task<string?> SingleThreadFirstHit()
    {
        using var sc = _sp.CreateScope();
        var dataLoader = ProvideDataLoader(sc);
        return await dataLoader.LoadAsync("abc2");
    }

    private IDataLoader<string, string> ProvideDataLoader(IServiceScope serviceScope)
    {
        return Version == "Original"
            ? serviceScope.ServiceProvider.GetRequiredService<CustomBatchDataLoader>()
            : serviceScope.ServiceProvider.GetRequiredService<CustomBatchDataLoader2>();
    }

    internal static async Task Test()
    {
        var b = new SingleThreadBenchmarks();
        b.Setup();

        b.Version = "Original";
        await TestSingleThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);

        b.Version = "vNext";
        await TestSingleThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);
    }

    private static async Task TestSingleThreadFirstHit(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadFirstHit();
        Asserts.Assert(result == "Value:abc2", b.Version, actual: result);
    }

    private static async Task TestMultiThreadCachedLoad(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadCached();
        Asserts.Assert(result == "Value:abc2", b.Version, actual: result);
    }
}

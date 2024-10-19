using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadBenchmarks
{
    private const int _keyCount = 50;
    private string[] _keys = null!;
    private Task<string?>[] _tasks = null!;
    private IServiceScope _customScope = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private ServiceProvider _sp = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDataLoader<CustomBatchDataLoader>();

        _sp = services.BuildServiceProvider();

        _keys = Enumerable.Range(0, _keyCount).Select(i => i.ToString()).ToArray();
        _tasks = new Task<string?>[_keyCount];
        _customScope = _sp.CreateScope();
        _dataLoaderCached = _customScope.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
    }

    [Benchmark]
    public Task<string?> SingleThreadCached()
    {
        return _dataLoaderCached.LoadAsync("abc2");
    }

    [Benchmark]
    public async Task<string?> SingleThreadFirstHit()
    {
        using var sc = _sp.CreateScope();
        var dataLoader = sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
        return await dataLoader.LoadAsync("abc2");
    }


    internal static async Task Test()
    {
        var b = new SingleThreadBenchmarks();
        b.Setup();
        await TestSingleThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);

    }

    private static async Task TestSingleThreadFirstHit(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadFirstHit();
        Asserts.Assert(result == "Value:abc2", result);
    }

    private static async Task TestMultiThreadCachedLoad(SingleThreadBenchmarks b)
    {
        var result = await b.SingleThreadCached();
        Asserts.Assert(result == "Value:abc2", result);
    }
}

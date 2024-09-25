using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadCachedBenchmarks
{
    private IBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoader = null!;
    private PromiseCacheOwner _promiseCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = AutoBatchScheduler.Default;
        _promiseCache = new PromiseCacheOwner();
        var options = new DataLoaderOptions
        {
            Cache = _promiseCache.Cache,
        };
        _dataLoader = new CustomBatchDataLoader(_scheduler, options);
    }

    [Benchmark]
    public Task<string?> SingleThreadCached()
    {
        return _dataLoader.LoadAsync("abc2");
    }

    [Benchmark]
    public async Task<string?> SingleThreadFirstHitCache()
    {
        using var promiseCache = new PromiseCacheOwner();
        var options = new DataLoaderOptions
        {
            Cache = promiseCache.Cache,
        };
        var dataLoader = new CustomBatchDataLoader(_scheduler, options);
        return await dataLoader.LoadAsync("abc2");
    }
}

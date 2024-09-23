using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadPerformanceBenchmarks
{
    private IBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoaderUncached = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private PromiseCacheOwner _promiseCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new AutoBatchScheduler();
        _dataLoaderUncached = new CustomBatchDataLoader(
            _scheduler,
            new DataLoaderOptions());
        _promiseCache = new PromiseCacheOwner();
        _dataLoaderCached  = new CustomBatchDataLoader(_scheduler, new DataLoaderOptions
        {
            Cache = _promiseCache.Cache,
        });
    }

    [Benchmark]
    public Task<string?> UncachedLoad()
    {
        return _dataLoaderUncached.LoadAsync("abc1");
    }

    [Benchmark]
    public Task<string?> CachedLoad()
    {
        return _dataLoaderCached.LoadAsync("abc2");
    }


    [Benchmark]
    public Task<string?> CachedLoadWithReset()
    {
        var cachedLoadWithReset = _dataLoaderCached.LoadAsync("abc2");
        _promiseCache.Cache.Clear();
        return cachedLoadWithReset;
    }
}

using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadCachedBenchmarks
{
    private IBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoader = null!;
    private PromiseCacheOwner _promiseCacheOwner = null!;
    private IPromiseCache _promiseCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = AutoBatchScheduler.Default;
        _promiseCacheOwner = new PromiseCacheOwner();
        _promiseCache = _promiseCacheOwner.Cache;
        var options = new DataLoaderOptions
        {
            Cache = _promiseCache,
        };
        _dataLoader = new CustomBatchDataLoader(_scheduler, options);
    }

    [Benchmark]
    public Task<string?> SingleThreadCached()
    {
        return _dataLoader.LoadAsync("abc2");
    }

    [Benchmark]
    public Task<string?> SingleThreadFirstHitCache()
    {
        _promiseCache.Clear();
        return _dataLoader.LoadAsync("abc2");
    }
}

using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MarkdownExporter]
[MemoryDiagnoser]
public class CachedBenchmarks
{
    private IBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private PromiseCacheOwner _promiseCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new AutoBatchScheduler();
        _promiseCache = new PromiseCacheOwner();
        _dataLoaderCached = new CustomBatchDataLoader(_scheduler, new DataLoaderOptions
        {
            Cache = _promiseCache.Cache,
        });
    }

    [Benchmark]
    public Task<string?> CachedLoadWithReset()
    {
        return _dataLoaderCached.LoadAsync("abc2");
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _promiseCache.Cache.Clear();
    }
}

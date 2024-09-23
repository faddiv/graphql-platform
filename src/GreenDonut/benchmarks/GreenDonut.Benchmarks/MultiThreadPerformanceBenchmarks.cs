using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class MultiThreadPerformanceBenchmarks
{
    private ManualBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoaderUncached = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private PromiseCacheOwner _promiseCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new ManualBatchScheduler();
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
    public async Task<string?> UncachedLoad()
    {
        var length = 50;
        var tasks = new Task<string?>[length];
        for (int i = 0; i < length; i++)
        {
            tasks[i] = _dataLoaderUncached.LoadAsync(i.ToString());
        }

        await _scheduler.DispatchAsync();
        await Task.WhenAll(tasks);
        return tasks[0].Result;
    }

    [Benchmark]
    public async Task<string?> CachedLoad()
    {
        var length = 50;
        var tasks = new Task<string?>[length];
        for (int i = 0; i < length; i++)
        {
            tasks[i] = _dataLoaderCached.LoadAsync(i.ToString());
        }

        await _scheduler.DispatchAsync();
        await Task.WhenAll(tasks);
        return tasks[0].Result;
    }
}

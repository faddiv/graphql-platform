using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class MultiThreadPerformanceBenchmarks
{
    private const int _keyCount = 50;
    private ManualBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoaderUncached = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private PromiseCacheOwner _promiseCache = null!;
    private string[] _keys = null!;
    Task<string?>[] _tasks = null!;

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

        _keys = Enumerable.Range(0, _keyCount).Select(i => i.ToString()).ToArray();
        _tasks = new Task<string?>[_keyCount];
    }

    [Benchmark]
    public async Task<string?> UncachedLoad()
    {
        for (int i = 0; i < _keyCount; i++)
        {
            _tasks[i] = _dataLoaderUncached.LoadAsync(_keys[i]);
        }

        await _scheduler.DispatchAsync();
        await Task.WhenAll(_tasks);
        return _tasks[0].Result;
    }

    [Benchmark]
    public async Task<string?> CachedLoad()
    {
        for (int i = 0; i < _keyCount; i++)
        {
            _tasks[i] = _dataLoaderCached.LoadAsync(_keys[i]);
        }

        await _scheduler.DispatchAsync();
        await Task.WhenAll(_tasks);
        return _tasks[0].Result;
    }
}

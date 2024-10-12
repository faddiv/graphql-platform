using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SubscriptionBenchmarks
{
    private readonly string[] _keys = Enumerable.Range(1, 10).Select(e => $"Key{e}").ToArray();
    private readonly IBatchScheduler _scheduler = AutoBatchScheduler.Default;
    private readonly PromiseCacheOwner _promiseCache = new PromiseCacheOwner();
    private CustomBatchDataLoader _dataLoader = null!;
    private int _notificationCount = 0;
    private Action<IPromiseCache, Promise<string>> _callback = null!;

    [GlobalSetup]
    public void Setup()
    {
        _callback = new(Handler);
        var options = new DataLoaderOptions
        {
            Cache = _promiseCache.Cache,
        };
        _dataLoader = new CustomBatchDataLoader(_scheduler, options);
    }

    [Benchmark]
    public Task<IReadOnlyList<string?>> SubscribeAndNotify()
    {
        _notificationCount = 0;
        _dataLoader.Clear();
        for (var i = 0; i < 16; i++)
        {
            _promiseCache.Cache.Subscribe(_callback, null);
        }
        return _dataLoader.LoadAsync(_keys);
    }

    public static async Task Test()
    {
        var benchmark = new SubscriptionBenchmarks();
        benchmark.Setup();
        var result = await benchmark.SubscribeAndNotify();
        Asserts.Assert(result.Count == 10, result.Count);
        Asserts.Assert(benchmark._notificationCount == 160, benchmark._notificationCount);
    }

    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Handler(IPromiseCache cache, Promise<string> promise)
    {
        Interlocked.Increment(ref _notificationCount);
    }
}

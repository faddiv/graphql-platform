using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using GreenDonut.Benchmarks.TestInfrastructure;
using GreenDonut.LoadTests.TestClasses;
using GreenDonutV2;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SubscriptionBenchmarks
{
    private readonly string[] _keys = Enumerable.Range(1, 10).Select(e => $"Key{e}").ToArray();
    private int _notificationCount = 0;
    private Action<IPromiseCache, Promise<string>> _callback = null!;
    private ServiceProvider _sp = null!;

    // ReSharper disable once MemberCanBePrivate.Global
    [Params(Defaults.Original, Defaults.VNext)]
    public string Version { get; set; } = "Original";

    [GlobalSetup]
    public void Setup()
    {
        _callback = new(Handler);

        _sp = Tests.CreateServiceProvider();

    }

    [Benchmark]
    public async Task<IReadOnlyList<string?>> SubscribeAndNotify()
    {
        using var sc = _sp.CreateScope();
        var dataLoader = Tests.ProvideDataLoader(sc, Version);
        var promiseCache = GetPromiseCache(sc);
        _notificationCount = 0;
        for (var i = 0; i < 16; i++)
        {
            promiseCache.Subscribe(_callback, null);
        }

        var task = dataLoader.LoadAsync(_keys);
        await Tests.GetManualBatchScheduler(sc).DispatchAsync();
        var result = await task;
        while (_notificationCount < 160)
        {
            await Task.Yield();
        }

        return result;
    }

    private IPromiseCache GetPromiseCache(IServiceScope sc)
    {
        return Version == "Original"
            ? sc.ServiceProvider.GetRequiredService<PromiseCacheOwner>().Cache
            : sc.ServiceProvider.GetRequiredService<PromiseCacheOwner2>().Cache;
    }

    public static async Task Test()
    {
        var b = new SubscriptionBenchmarks();
        b.Setup();

        b.Version = "Original";
        await TestSubscribeAndNotify(b);

        b.Version = "vNext";
        await TestSubscribeAndNotify(b);
    }

    private static async Task TestSubscribeAndNotify(SubscriptionBenchmarks benchmark)
    {
        var result = await benchmark.SubscribeAndNotify();
        Asserts.Assert(result.Count == 10, benchmark.Version, actual: result.Count);
        Asserts.Assert(benchmark._notificationCount == 160, benchmark.Version, actual: benchmark._notificationCount);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Handler(IPromiseCache cache, Promise<string> promise)
    {
        Interlocked.Increment(ref _notificationCount);
    }
}

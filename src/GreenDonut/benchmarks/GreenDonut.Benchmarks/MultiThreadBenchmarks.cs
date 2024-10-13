using System.Collections.Concurrent;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class MultiThreadBenchmarks
{
    private const int _keyCount = 50;
    private string[] _keys = null!;
    private Task<string?>[] _tasks = null!;
    private IServiceScope _customScope = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private ManualBatchScheduler _schedulerCached = null!;
    private ServiceProvider _sp = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddScoped<IBatchScheduler, ManualBatchScheduler>();
        services.AddDataLoader<CustomBatchDataLoader>();

        _sp = services.BuildServiceProvider();

        _keys = Enumerable.Range(0, _keyCount).Select(i => i.ToString()).ToArray();
        _tasks = new Task<string?>[_keyCount];
        _customScope = _sp.CreateScope();
        _dataLoaderCached = _customScope.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
        _schedulerCached = (ManualBatchScheduler)_customScope.ServiceProvider.GetRequiredService<IBatchScheduler>();
    }

    [Benchmark]
    public async Task<string?[]> MultiThreadCachedLoad()
    {
        for (int i = 0; i < _keyCount; i++)
        {
            _tasks[i] = _dataLoaderCached.LoadAsync(_keys[i]);
        }

        await _schedulerCached.DispatchAsync();
        return await Task.WhenAll(_tasks);
    }

    [Benchmark]
    public async Task<string?[]> MultiThreadFirstHit()
    {
        using var sc = _sp.CreateScope();
        var dataLoader = sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
        var batchScheduler = (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();
        for (int i = 0; i < _keyCount; i++)
        {
            _tasks[i] = dataLoader.LoadAsync(_keys[i]);
        }

        await batchScheduler.DispatchAsync();
        return await Task.WhenAll(_tasks);
    }

    [Benchmark]
    public async Task<string?[]> MultiThreadLoadFullBatch()
    {
        var ct = CancellationToken.None;
        using var sc = _sp.CreateScope();

        var dataLoader = sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
        var batchScheduler = (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();

        var count = 100;
        var tasks = new Task<string?>[count];
        var runs = new ConcurrentStack<string>();
        for (int i = 0; i < count; i++)
        {
            var index = i;
            var contextNumber = Random.Shared.Next(0, 20) + 1;
            var key = $"Key{contextNumber}";

            tasks[index] = Task.Run(async () =>
            {
                Task<string?> task;

                try
                {
                    task = dataLoader.LoadAsync(key, ct);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    runs.Push(key);
                }
                return await task;
            });
        }
        var time = Stopwatch.GetTimestamp();
        while (runs.Count < count)
        {
            if (Stopwatch.GetElapsedTime(time) > TimeSpan.FromMilliseconds(500))
            {
                throw new Exception("Start threads timed out.");
            }
            await Task.Yield();
        }
        await batchScheduler.DispatchAsync(ct);
        Task.WaitAll(tasks, 500, ct);
        var completed = tasks.Count(e => e.IsCompleted);
        if (completed == count)
        {
            return tasks.Select(e => e.Result).ToArray();
        }
        else
        {
            throw new Exception("Not all threads finished.");
        }
    }

    internal static async Task Test()
    {
        var b = new MultiThreadBenchmarks();
        b.Setup();
        await TestMultiThreadFirstHit(b);
        await TestMultiThreadCachedLoad(b);
        await TestMultiThreadLoadFullBatch(b);

    }

    private static async Task TestMultiThreadFirstHit(MultiThreadBenchmarks b)
    {
        var result = await b.MultiThreadFirstHit();
        Asserts.Assert(result.Length == _keyCount, result.Length);
    }

    private static async Task TestMultiThreadCachedLoad(MultiThreadBenchmarks b)
    {
        var result = await b.MultiThreadCachedLoad();
        Asserts.Assert(result.Length == _keyCount, result.Length);
    }

    private static async Task TestMultiThreadLoadFullBatch(MultiThreadBenchmarks b)
    {
        var result = await b.MultiThreadLoadFullBatch();
        Asserts.Assert(result.Length == 100, result.Length);
    }
}

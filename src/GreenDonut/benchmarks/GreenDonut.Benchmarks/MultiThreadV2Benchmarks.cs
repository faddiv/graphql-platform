using System.Collections.Concurrent;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class MultiThreadV2Benchmarks
{
    private const int _keyCount = 50;
    private ManualBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoaderCached = null!;
    private PromiseCacheOwner _promiseCache = null!;
    private string[] _keys = null!;
    Task<string?>[] _tasks = null!;
    private ServiceProvider _sp = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddScoped<IBatchScheduler, ManualBatchScheduler>();
        services.AddDataLoader<CustomBatchDataLoader>();

        _sp = services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task<string?[]> MultiThreadLoadBatch()
    {
        var ct = CancellationToken.None;
        using var sc = _sp.CreateScope();

        var dataLoader = sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
        var batchScheduler = (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();

        var count = 100;
        var tasks = new Task<string?>[count];
        var runs = new ConcurrentStack<string>();
        var time = Stopwatch.GetTimestamp();
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
        var b = new MultiThreadV2Benchmarks();
        b.Setup();
        var result = await b.MultiThreadLoadBatch();

        Asserts.Assert(result.Length == 100, result.Length);
    }
}

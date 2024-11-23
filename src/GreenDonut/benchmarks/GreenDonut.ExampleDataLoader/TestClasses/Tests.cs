using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using GreenDonut.ExampleDataLoader.TestClasses.TestHelpers;
using GreenDonut.ExampleDataLoader.TestedImplementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace GreenDonut.ExampleDataLoader.TestClasses;

public static class Tests
{
    private static readonly DefaultObjectPool<RunCounter> _runCounterPool = new(new RunCounterObjectPoolPolicy());
    private static readonly ConcurrentDictionary<int, string> _keyPool = new();

    public static async Task<Result> ExecuteTestWith(
        ServiceProvider serviceProvider,
        string version,
        CancellationToken ct)
    {
        using var sc = serviceProvider.CreateScope();
        return await ExecuteTestWith(sc, version, ct);
    }

    public static async Task<Result> ExecuteTestWith(
        IServiceScope sc,
        string version,
        CancellationToken ct)
    {
        var dataLoader = ProvideDataLoader(sc, version);
        var batchScheduler = GetManualBatchScheduler(sc);

        const int count = 100;
        var tasks = ArrayPool<Task?>.Shared.Rent(count);
        var runCounter = _runCounterPool.Get();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var contextNumber = Random.Shared.Next(0, 20) + 1;
                var key = _keyPool.GetOrAdd(contextNumber, static i1 => $"Key{i1}");

                tasks[i] = RunParallelDataLoad(dataLoader, key, i, runCounter, ct);
            }

            var time = Stopwatch.GetTimestamp();
            while (runCounter.StartedCount < count)
            {
                if (Stopwatch.GetElapsedTime(time) > TimeSpan.FromMilliseconds(500))
                {
                    return new Result(500, "Deadlock");
                }

                await Task.Yield();
            }

            await batchScheduler.DispatchAsync(ct);
            var time1 = Stopwatch.GetTimestamp();
            await Helpers.WaitAll(tasks, 500, ct);
            var elapsed = Stopwatch.GetElapsedTime(time1);
            var completed = tasks.Count(e => e?.IsCompleted ?? false);
            if (completed < count)
            {
                return new Result(501, $"Failed thread: {count - completed} elapsed: {elapsed}");
            }

            return runCounter.AllSucceeded
                ? Result.Ok
                : new Result(500, $"Failed on {runCounter.FailCount} elapsed: {elapsed}");
        }
        finally
        {
            ArrayPool<Task?>.Shared.Return(tasks);
            _runCounterPool.Return(runCounter);
        }
    }

    public static ManualBatchScheduler GetManualBatchScheduler(IServiceScope sc)
    {
        return (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();
    }

    private static async Task RunParallelDataLoad(
        IDataLoader<string, string> dataLoader,
        string key,
        int index,
        RunCounter runCounter,
        CancellationToken ct)
    {
        await Task.Yield();
        try
        {
            var task = dataLoader.LoadAsync(key, ct);
            runCounter.Increment();
            var result = await task;
            if (result?.StartsWith("Value:") == true &&
                result.EndsWith(key))
            {
                runCounter.Finished(index, true);
            }
            else
            {
                runCounter.Finished(index, false);
            }
        }
        catch (OperationCanceledException)
        {
            runCounter.Finished(index, true);
        }
        catch (Exception)
        {
            runCounter.Finished(index, false);
        }
    }

    public static IDataLoader<string, string> ProvideDataLoader(IServiceScope sc, string version)
    {
        return version == Defaults.Original
            ? sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>()
            : sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader2>();
    }

    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IBatchScheduler, ManualBatchScheduler>();
        services.TryAddDataLoader2Core();
        services.AddDataLoader<CustomBatchDataLoader>();
        services.AddDataLoader<CustomBatchDataLoader2>();
        return services.BuildServiceProvider();
    }
}

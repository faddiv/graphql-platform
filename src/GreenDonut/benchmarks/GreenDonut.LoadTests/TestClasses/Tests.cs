using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.LoadTests.TestClasses;

public class Tests
{
    public static async Task<Result> ExecuteTestWith(
        ServiceProvider serviceProvider,
        string version,
        CancellationToken ct)
    {
        using var sc = serviceProvider.CreateScope();

        var dataLoader = ProvideDataLoader(sc, version);
        var batchScheduler = (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();

        const int count = 100;
        var tasks = new Task[count];
        var runs = new ConcurrentStack<bool>();
        int countRuns = 0;
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var contextNumber = Random.Shared.Next(0, 20) + 1;
            var key = $"Key{contextNumber}";

            tasks[index] = Task.Run(async () =>
            {
                try
                {
                    var task =  dataLoader.LoadAsync(key, ct);
                    Interlocked.Increment(ref countRuns);
                    var result = await task;
                    if (result != null &&
                        result.StartsWith("Value:") &&
                        result.EndsWith(key))
                    {
                        runs.Push(true);
                    }
                    else
                    {
                        runs.Push(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    runs.Push(true);
                }
                catch (Exception)
                {
                    runs.Push(false);
                }
            }, ct);
        }

        var time = Stopwatch.GetTimestamp();
        while (countRuns < count)
        {
            if (Stopwatch.GetElapsedTime(time) > TimeSpan.FromMilliseconds(500))
            {
                return new Result(500, "Deadlock");
            }

            await Task.Yield();
        }

        await batchScheduler.DispatchAsync(ct);
        var time1 = Stopwatch.GetTimestamp();
        Task.WaitAll(tasks, 500, ct);
        var elapsed = Stopwatch.GetElapsedTime(time1);
        var completed = tasks.Count(e => e.IsCompleted);
        if (completed < count)
        {
            return new Result(501, $"Failed thread: {count - completed} elapsed: {elapsed}");
        }

        if (runs.All(success => success))
        {
            return new Result(200);
        }
        else
        {
            return new Result(500, $"Failed on {runs.Count(e => !e)} elapsed: {elapsed}");
        }
    }

    private static IDataLoader<string, string> ProvideDataLoader(IServiceScope sc, string version)
    {
        return version == "Original"
            ? sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>()
            : sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader2>();
    }
}

public record Result(int StatusCode, string? Message = null);

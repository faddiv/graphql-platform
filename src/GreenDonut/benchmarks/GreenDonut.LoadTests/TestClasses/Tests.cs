using System.Collections.Concurrent;
using System.Diagnostics;
using GreenDonut.Benchmarks;
using GreenDonut.LoadTests.LoadTesting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GreenDonut.LoadTests.TestClasses;

public static class Tests
{
    public static async Task ExecuteNTimes(
        ServiceProvider serviceProvider,
        int count,
        string version,
        CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            var result = await ExecuteTestWith(serviceProvider, version, ct);
            if (result.StatusCode != 200)
            {
                AnsiConsole.MarkupLine($"[red]Failed[/] {result.StatusCode} {result.Message}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Success[/]");
            }
        }
    }

    public static async Task RunWithCustomRunner(ServiceProvider serviceProvider)
    {
        var root = new TestRunnerHost();
        for (var i = 0; i < 1; i++)
        {
            root.StartTestRunner(new CustomTestRunner(root, serviceProvider));
        }

        await Task.Delay(1000 * 60);

        await root.Stop();
    }

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
        var countRuns = 0;
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
                    if (result?.StartsWith("Value:") == true &&
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
            return Result.Ok;
        }
        else
        {
            return new Result(500, $"Failed on {runs.Count(e => !e)} elapsed: {elapsed}");
        }
    }

    private static IDataLoader<string, string> ProvideDataLoader(IServiceScope sc, string version)
    {
        return version == Defaults.Original
            ? sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>()
            : sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader2>();
    }
}

public record Result(int StatusCode, string? Message = null)
{
    public static Result Ok { get; } = new(200);
}

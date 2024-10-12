using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using GreenDonut;
using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;
using NBomber.CSharp;
using Spectre.Console;

Console.WriteLine("Hello, World!");

var services = new ServiceCollection();
services.AddScoped<IBatchScheduler, ManualBatchScheduler>();
services.AddDataLoader<CustomBatchDataLoader>();

var sp = services.BuildServiceProvider();

var scenario = Scenario.Create("load_batch", async context =>
{
    var ct = context.ScenarioCancellationToken;
    using var sc = sp.CreateScope();

    var dataLoader = sc.ServiceProvider.GetRequiredService<CustomBatchDataLoader>();
    var batchScheduler = (ManualBatchScheduler)sc.ServiceProvider.GetRequiredService<IBatchScheduler>();

    var count = 100;
    var tasks = new Task<string?>[count];
    var runs = new ConcurrentStack<string>();
    var time = Stopwatch.GetTimestamp();
    for (int i = 0; i < count; i++)
    {
        var index = i;
        var contextNumber = context.Random.Next(0, 20) + 1;
        var key = $"Key{contextNumber}";

        tasks[index] = Task.Run(async () =>
        {
            Task<string?> task;

            try
            {
                task = dataLoader.LoadAsync(key, ct);
            }
            catch(OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                context.StopCurrentTest($"Failed to start: {ex.ToString()}");
                return null;
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
            return Response.Fail(statusCode: "500", message: "Deadlock");
        }
        await Task.Yield();
    }
    await batchScheduler.DispatchAsync(ct);
    Task.WaitAll(tasks, 500, ct);
    var completed = tasks.Count(e => e.IsCompleted);
    if (completed == count)
    {
        return Response.Ok();
    }
    else
    {
        return Response.Fail(statusCode: "501", message: $"Failed thread: {count - completed}");
    }

})
.WithLoadSimulations(
    Simulation.KeepConstant(
        copies: 10,
        during: TimeSpan.FromMinutes(5))
).WithWarmUpDuration(TimeSpan.FromSeconds(5));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();


/*
var testRoot = new TestRunnerHost();
for (int i = 0; i < 10; i++)
{
    var tester = new TestRunnerBase(testRoot);
    testRoot.StartTestRunner(tester);
}

Console.ReadKey(true);

await testRoot.Stop();
*/

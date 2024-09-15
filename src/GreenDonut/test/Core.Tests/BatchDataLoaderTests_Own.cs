using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace GreenDonut;

public class BatchDataLoaderTests_Own(ITestOutputHelper testOutputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    [Fact]
    public async Task LoadTheSameKeyOnDifferentScheduleYieldTheSameResult()
    {
        // arrange
        var batchScheduler = new ManualBatchScheduler();
        var cacheOwner = new PromiseCacheOwner();
        var dataLoader = new CustomBatchDataLoader(
            batchScheduler,
            new DataLoaderOptions
            {
                Cache = cacheOwner.Cache,
                CancellationToken = cacheOwner.CancellationToken,
            });

        // act
        var task1 = dataLoader.LoadAsync("1abc");
        await batchScheduler.DispatchAsync();
        var result1 = await task1;
        var task2 = dataLoader.LoadAsync("1abc");
        await batchScheduler.DispatchAsync();
        var result2 = await task2;

        // assert
        Assert.Same(result1, result2);
    }

    [Fact]
    public async Task LoadTheSameKeyTwiceWillYieldSameResult()
    {
        // arrange
        var dataLoader = new CustomBatchDataLoader(
            new DelayDispatcher(),
            new DataLoaderOptions());

        // act
        var results = await Task.WhenAll(dataLoader.LoadAsync("1abc"), dataLoader.LoadAsync("1abc"));

        // assert
        Assert.Same(results[0], results[1]);
    }

    [Fact]
    public async Task LoadDifferentKeysBatchedTogether()
    {
        // arrange
        var batchScheduler = new ManualBatchScheduler();
        var dataLoader = new CustomBatchDataLoader(
            batchScheduler,
            new DataLoaderOptions());

        // act
        var task1 = dataLoader.LoadAsync("1abc");
        var task2 = dataLoader.LoadAsync("2abc");
        await batchScheduler.DispatchAsync();

        // assert
        Assert.Equal(1, dataLoader.ExecutionCount);
    }

    [Fact]
    public async Task Ensure_Large_Number_Of_Batches_With_Different_Values_Can_Be_Enqueued_Concurrently_Async()
    {
        // arrange
        using var cts = new CancellationTokenSource(5000);
        var ct = cts.Token;
        var services = new ServiceCollection()
            .AddScoped<IBatchScheduler, DelayDispatcher>()
            .AddDataLoader<TestDataLoader>()
            .BuildServiceProvider();
        var dataLoader = services.GetRequiredService<TestDataLoader>();

        var sw = Stopwatch.StartNew();
        // act
        List<Task> tasks = new();
        foreach (var ii in Enumerable.Range(0, 5000))
        {
            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        var result = await dataLoader.LoadAsync(ii, ct);

                        // assert
                        Assert.Equal(500, result?.Length ?? 0);

                    },
                    ct));
        }

        await Task.WhenAll(tasks);
        sw.Stop();
        _testOutputHelper.WriteLine($"Elapsed: {sw.Elapsed} ExecutionCount: {dataLoader._executionCount}");
    }

    [Fact]
    public async Task Ensure_Large_Number_Of_Batches_With_Few_Values_Can_Be_Enqueued_Concurrently_Async()
    {
        // arrange
        using var cts = new CancellationTokenSource(5000);
        var ct = cts.Token;
        var services = new ServiceCollection()
            .AddScoped<IBatchScheduler, DelayDispatcher>()
            .AddDataLoader<TestDataLoader>()
            .BuildServiceProvider();
        var dataLoader = services.GetRequiredService<TestDataLoader>();

        var sw = Stopwatch.StartNew();
        // act
        List<Task> tasks = new();
        foreach (var ii in Enumerable.Range(0, 5000))
        {
            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        var result = await dataLoader.LoadAsync(ii%10, ct);

                        // assert
                        Assert.Equal(500, result?.Length ?? 0);

                    },
                    ct));
        }

        await Task.WhenAll(tasks);
        sw.Stop();
        _testOutputHelper.WriteLine($"Elapsed: {sw.Elapsed} ExecutionCount: {dataLoader._executionCount}");
    }

    private sealed class TestDataLoader(
        IBatchScheduler batchScheduler,
        DataLoaderOptions options)
        : BatchDataLoader<int, int[]>(batchScheduler, options)
    {
        public int _executionCount = 0;
        protected override async Task<IReadOnlyDictionary<int, int[]>> LoadBatchAsync(
            IReadOnlyList<int> runNumbers,
            CancellationToken cancellationToken)
        {
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _executionCount);
            return runNumbers
                .Select(t => (t, Enumerable.Range(0, 500)))
                .ToDictionary(t => t.Item1, t => t.Item2.ToArray());
        }
    }

    public class CustomBatchDataLoader(IBatchScheduler batchScheduler, DataLoaderOptions options)
        : BatchDataLoader<string, string>(batchScheduler, options)
    {
        private int _executionCount;
        public int ExecutionCount => _executionCount;

        protected override async Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
        {
            await Task.Delay(25, cancellationToken);
            Interlocked.Increment(ref _executionCount);
            return keys.ToDictionary(t => t, t => "Value:" + t);
        }
    }
}

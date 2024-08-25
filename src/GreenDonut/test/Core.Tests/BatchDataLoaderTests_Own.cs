using Xunit;

namespace GreenDonut;

public class BatchDataLoaderTests_Own
{
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

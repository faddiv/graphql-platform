using GreenDonut;
using GreenDonutV2.TestInfrastructure;
using Xunit;

namespace GreenDonutV2;

public class GroupDataLoaderTests
{
    [Fact]
    public async Task LoadSingleAsync()
    {
        // arrange
        var dataLoader = new CustomBatchDataLoader(
            new AutoBatchScheduler(),
            new DataLoaderOptions2());

        // act
        var result = await dataLoader.LoadRequiredAsync("abc");

        // assert
        Assert.Collection(result, t => Assert.Equal("Value:abc", t));
    }

    [Fact]
    public async Task LoadTwoAsync()
    {
        // arrange
        var dataLoader = new CustomBatchDataLoader(
            new DelayDispatcher(),
            new DataLoaderOptions2());

        // act
        var result1 = dataLoader.LoadRequiredAsync("1abc");
        var result2 = dataLoader.LoadRequiredAsync("0abc");

        // assert
        Assert.Collection(await result1, t => Assert.Equal("Value:1abc", t));
        Assert.Collection(await result2, t => Assert.Equal("Value:0abc", t));
    }

    public class CustomBatchDataLoader(
        IBatchScheduler batchScheduler,
        DataLoaderOptions2 options)
        : GroupedDataLoader2<string, string>(batchScheduler, options)
    {
        protected override Task<ILookup<string, string>> LoadGroupedBatchAsync(
            IReadOnlyList<string> keys,
            CancellationToken cancellationToken)
            => Task.FromResult(keys.ToLookup(t => t, t => "Value:" + t));
    }
}

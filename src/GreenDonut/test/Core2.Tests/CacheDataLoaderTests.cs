using Xunit;

namespace GreenDonutV2;

public class CacheDataLoaderTests
{
    [Fact]
    public async Task LoadSingleAsync()
    {
        // arrange
        using var cacheOwner = new PromiseCacheOwner2();
        var dataLoader = new CustomCacheDataLoader(
            new DataLoaderOptions2
            {
                Cache = cacheOwner.Cache
            });

        // act
        var result = await dataLoader.LoadAsync("abc");

        // assert
        Assert.Equal("Value:abc", result);
    }

    public class CustomCacheDataLoader(DataLoaderOptions2 options)
        : CacheDataLoader2<string, string>(options)
    {
        protected override Task<string> LoadSingleAsync(
            string key,
            CancellationToken cancellationToken)
            => Task.FromResult("Value:" + key);
    }
}

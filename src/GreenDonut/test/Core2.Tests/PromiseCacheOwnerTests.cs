using Xunit;

namespace GreenDonutV2;

public class PromiseCacheOwnerTests
{
    [Fact]
    public void EnsureTaskCacheIsReused()
    {
        // arrange
        var pool = PromiseCachePool2.Create();
        var cacheOwner1 = new PromiseCacheOwner2(pool);
        var cache = cacheOwner1.Cache;
        cacheOwner1.Dispose();

        // act
        using var cacheOwner2 = new PromiseCacheOwner2(pool);

        // assert
        Assert.Same(cache, cacheOwner2.Cache);
    }

    [Fact]
    public void EnsureNewCachesAreIssued()
    {
        // arrange
        var pool = PromiseCachePool2.Create();

        // act
        using var cacheOwner1 = new PromiseCacheOwner2(pool);
        using var cacheOwner2 = new PromiseCacheOwner2(pool);

        // assert
        Assert.NotSame(cacheOwner1.Cache, cacheOwner2.Cache);
    }

    [Fact]
    public void DisposingTwoTimesWillNotThrow()
    {
        var cacheOwner = new PromiseCacheOwner2();
        cacheOwner.Dispose();
        cacheOwner.Dispose();
    }
}

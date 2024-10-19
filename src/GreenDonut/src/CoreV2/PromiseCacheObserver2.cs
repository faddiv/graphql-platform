using GreenDonut;

namespace GreenDonutV2;

/// <summary>
/// Provides factory methods to create <see cref="IPromiseCacheObserver"/>s.
/// </summary>
public static class PromiseCacheObserver2
{
    /// <summary>
    /// Creates a <see cref="IPromiseCacheObserver"/> that creates lookups.
    /// </summary>
    /// <param name="createLookup">
    /// A delegate to create a lookup key from the cached value.
    /// </param>
    /// <param name="dataLoader">
    /// The data loader that observes the cache.
    /// </param>
    /// <typeparam name="TKey">
    /// The type of the lookup key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the cached value.
    /// </typeparam>
    /// <returns>
    /// Returns a new instance of <see cref="IPromiseCacheObserver"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Throws if <paramref name="createLookup"/> is <c>null</c> or
    /// if <paramref name="dataLoader"/> is <c>null</c>.
    /// </exception>
    public static IPromiseCacheObserver Create<TKey, TValue>(
        Func<TValue, TKey> createLookup,
        DataLoaderBase2<TKey, TValue> dataLoader)
        where TKey : notnull
    {
        if (createLookup == null)
        {
            throw new ArgumentNullException(nameof(createLookup));
        }

        if (dataLoader == null)
        {
            throw new ArgumentNullException(nameof(dataLoader));
        }

        return new PromiseCacheObserver2<TKey, TValue>(createLookup, dataLoader.CacheKeyType);
    }

    /// <summary>
    /// Creates a <see cref="IPromiseCacheObserver"/> that creates new cache entries from existing cache entries.
    /// </summary>
    /// <param name="createLookup">
    /// A delegate to create a lookup key from the cached value.
    /// </param>
    /// <param name="dataLoader">
    /// The data loader that observes the cache.
    /// </param>
    /// <typeparam name="TKey">
    /// The type of the lookup key.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the cached value.
    /// </typeparam>
    /// <typeparam name="TObservedValue">
    /// The type of the observed value.
    /// </typeparam>
    /// <returns>
    /// Returns a new instance of <see cref="IPromiseCacheObserver"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Throws if <paramref name="createLookup"/> is <c>null</c> or
    /// if <paramref name="dataLoader"/> is <c>null</c>.
    /// </exception>
    public static IPromiseCacheObserver Create<TKey, TValue, TObservedValue>(
        Func<TObservedValue, KeyValuePair<TKey, TValue>?> createLookup,
        DataLoaderBase2<TKey, TValue> dataLoader)
        where TKey : notnull
    {
        if (createLookup == null)
        {
            throw new ArgumentNullException(nameof(createLookup));
        }

        if (dataLoader == null)
        {
            throw new ArgumentNullException(nameof(dataLoader));
        }

        return new PromiseCacheObserver2<TKey, TValue, TObservedValue>(createLookup, dataLoader.CacheKeyType);
    }
}

internal sealed class PromiseCacheObserver2<TKey, TValue> : PromiseCacheObserver<TValue> where TKey : notnull
{
    private readonly Func<TValue, TKey> _createLookup;
    private readonly string _cacheKeyType;

    internal PromiseCacheObserver2(Func<TValue, TKey> createLookup, string cacheKeyType)
    {
        _createLookup = createLookup ?? throw new ArgumentNullException(nameof(createLookup));
        _cacheKeyType = cacheKeyType ?? throw new ArgumentNullException(nameof(cacheKeyType));
    }

    public override void OnNext(IPromiseCache cache, Promise<TValue> promise)
    {
        var privateKey = _createLookup(promise.Task.Result);
        var cacheKey = new PromiseCacheKey(_cacheKeyType, privateKey);
        cache.TryAdd(cacheKey, promise);
    }
}

internal sealed class PromiseCacheObserver2<TKey, TValue, TObservedValue>
    : PromiseCacheObserver<TObservedValue>
    where TKey : notnull
{
    private readonly Func<TObservedValue, KeyValuePair<TKey, TValue>?> _createLookup;
    private readonly string _cacheKeyType;

    internal PromiseCacheObserver2(Func<TObservedValue, KeyValuePair<TKey, TValue>?> createLookup, string cacheKeyType)
    {
        _createLookup = createLookup ?? throw new ArgumentNullException(nameof(createLookup));
        _cacheKeyType = cacheKeyType ?? throw new ArgumentNullException(nameof(cacheKeyType));
    }

    public override void OnNext(IPromiseCache cache, Promise<TObservedValue> promise)
    {
        var keyValuePair = _createLookup(promise.Task.Result);
        if (keyValuePair.HasValue)
        {
            var cacheKey = new PromiseCacheKey(_cacheKeyType, keyValuePair.Value.Key);
            cache.TryAdd(cacheKey, new Promise<TValue>(keyValuePair.Value.Value));
        }
    }
}

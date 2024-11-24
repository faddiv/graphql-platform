using GreenDonut;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
{
    /// <inheritdoc />
    public Task<TValue?> LoadAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cacheKeyType = CacheKeyType;

        var promise = CreateAndCachePromise(key, cacheKeyType, cancellationToken);

        EnsureBatchExecuted(_currentBatch, cacheKeyType, cancellationToken);
        return promise.Task;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TValue?>> LoadAsync(
        IReadOnlyCollection<TKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (keys.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<TValue?>>([]);
        }

        var cacheKeyType = CacheKeyType;

        var tasks = new Task<TValue?>[keys.Count];
        var index = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var promise = CreateAndCachePromise(key, cacheKeyType, cancellationToken);

            tasks[index++] = promise.Task;
        }

        EnsureBatchExecuted(_currentBatch, cacheKeyType, cancellationToken);

        return WhenAll(tasks);

        static async Task<IReadOnlyList<TValue?>> WhenAll(Task<TValue?>[] tasks)
            => await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private Promise<TValue?> CreateAndCachePromise(
        TKey key,
        string cacheKeyType,
        CancellationToken cancellationToken)
    {
        var cacheKey = new PromiseCacheKey(cacheKeyType, key);

        Promise<TValue?> promise;
        if (Cache is null)
        {
            promise = CreatePromiseFromBatch(cacheKey, cancellationToken);
        }
        else
        {
            if (Cache.TryGetOrAddPromise(cacheKey,
                CreatePromise,
                (this, cancellationToken), out promise))
            {
                _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
            }
        }

        return promise;

        static Promise<TValue?> CreatePromise(
            PromiseCacheKey key,
            (DataLoaderBase2<TKey, TValue>, CancellationToken) state)
        {
            var (self, cancellationToken) = state;
            return self.CreatePromiseFromBatch(key, cancellationToken);
        }
    }

    private Promise<TValue?> CreatePromiseFromBatch(
        PromiseCacheKey key,
        CancellationToken cancellationToken)
    {
        var currentBranch = _currentBatch ?? CreateNewBatch(null);
        while (true)
        {
            if (currentBranch.TryGetOrCreatePromise(
                key,
                AllowCachePropagation,
                out Promise<TValue?>? promise))
            {
                return promise.Value;
            }

            EnsureBatchExecuted(currentBranch, key.Type, cancellationToken);
            currentBranch = CreateNewBatch(currentBranch);
        }
    }
}

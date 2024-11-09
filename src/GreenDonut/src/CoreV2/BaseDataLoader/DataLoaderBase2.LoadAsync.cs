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

        var promise = CreateAndCachePromise(key, cancellationToken);

        EnsureBatchExecuted(_currentBatch, cancellationToken);
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

        var tasks = new Task<TValue?>[keys.Count];
        var index = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var promise = CreateAndCachePromise(key, cancellationToken);

            tasks[index++] = promise.Task;
        }

        EnsureBatchExecuted(_currentBatch, cancellationToken);

        return WhenAll(tasks);

        static async Task<IReadOnlyList<TValue?>> WhenAll(Task<TValue?>[] tasks)
            => await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private Promise<TValue?> CreateAndCachePromise(
        TKey key,
        CancellationToken cancellationToken)
    {
        var cacheKey = new PromiseCacheKey(CacheKeyType, key);

        Promise<TValue?> promise;
        if (Cache?.TryGetPromise(cacheKey, out promise) ?? false)
        {
            _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
            return promise;
        }

        var currentBranch = _currentBatch ?? CreateNewBatch(null);
        while (true)
        {
            if (currentBranch.TryGetOrCreatePromise(
                key,
                AllowCachePropagation,
                out Promise<TValue?> promise1))
            {
                promise = promise1;
                break;
            }

            EnsureBatchExecuted(currentBranch, cancellationToken);
            currentBranch = CreateNewBatch(currentBranch);
        }

        return promise;
    }
}

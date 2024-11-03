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
        if (Cache is null)
        {
            promise = CreatePromiseFromBatch(key, cancellationToken);
        }
        else
        {
            if (Cache.TryGetOrAddPromise(cacheKey, CreatePromise, (object?)null, out promise))
            {
                _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
            }
            else
            {
                if (IsDefault(promise))
                {
                    promise = CreatePromiseFromBatch(key, cancellationToken);
                }
                else
                {
                    AddPromiseToBatch(key, promise, cancellationToken);
                }
            }
        }

        return promise;
    }

    private Promise<TValue?> CreatePromiseFromBatch(
        TKey key,
        CancellationToken cancellationToken)
    {
        var currentBranch = _currentBatch ?? CreateNewBatch(null);
        while (true)
        {
            if (currentBranch.TryGetOrCreatePromise(
                key,
                AllowCachePropagation,
                out Promise<TValue?> promise))
            {
                return promise;
            }

            EnsureBatchExecuted(currentBranch, cancellationToken);
            currentBranch = CreateNewBatch(currentBranch);
        }
    }

    private void AddPromiseToBatch(
        TKey key,
        Promise<TValue?> promise,
        CancellationToken cancellationToken)
    {
        IPromise ipromise = promise;
        var currentBranch = _currentBatch ?? CreateNewBatch(null);
        while (true)
        {
            if (currentBranch.TryAddPromise(
                key,
                ipromise))
            {
                return;
            }

            EnsureBatchExecuted(currentBranch, cancellationToken);
            currentBranch = CreateNewBatch(currentBranch);
        }
    }

    private static bool IsDefault(Promise<TValue?> promise)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        return promise.Task is null;
    }

    private static Promise<TValue?> CreatePromise(PromiseCacheKey promiseCacheKey, object? o)
    {
        return Promise<TValue?>.Create();
    }
}

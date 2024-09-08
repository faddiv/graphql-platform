using System.Collections.Immutable;
using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using GreenDonut.Projections;
#endif

namespace GreenDonut;

public abstract partial class DataLoaderBase2<TKey, TValue>
    : IDataLoader<TKey, TValue>
    where TKey : notnull
{
    private readonly IDataLoaderDiagnosticEvents _diagnosticEvents;
    private readonly CancellationToken _ct;
    private readonly IBatchScheduler _batchScheduler;
    private readonly int _maxBatchSize;
    private readonly ConcurrentDictionary<PromiseCacheKey, IPromise> _cache;
    private volatile Batch2<TKey>? _currentBatch;

#if NET8_0_OR_GREATER
    private readonly ConcurrentDictionary<string, ISelectionDataLoader<TKey, TValue>> _branches;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="DataLoaderBase{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="batchScheduler">
    /// A scheduler to tell the <c>DataLoader</c> when to dispatch buffered batches.
    /// </param>
    /// <param name="options">
    /// An options object to configure the behavior of this particular
    /// <see cref="DataLoaderBase{TKey, TValue}"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Throws if <paramref name="options"/> is <c>null</c>.
    /// </exception>
    protected DataLoaderBase2(IBatchScheduler batchScheduler, DataLoaderOptions? options = null)
    {
        options ??= new DataLoaderOptions();
        _diagnosticEvents = options.DiagnosticEvents ?? NoopDataLoaderDiagnosticEventListener.Default;
        _ct = options.CancellationToken;
        _batchScheduler = batchScheduler;
        _maxBatchSize = options.MaxBatchSize;
        CacheKeyType = GetCacheKeyType(GetType());
        _cache = new ConcurrentDictionary<PromiseCacheKey, IPromise>();
#if NET8_0_OR_GREATER
        _branches = new ConcurrentDictionary<string, ISelectionDataLoader<TKey, TValue>>();
#endif
    }

    private protected virtual bool AllowCachePropagation => true;

    private protected virtual bool AllowBranching => true;

    /// <summary>
    /// Gets the cache key type for this DataLoader.
    /// </summary>
    protected internal virtual string CacheKeyType { get; }

    public IImmutableDictionary<string, object?> ContextData { get; set; }

    public Task<TValue?> LoadAsync(TKey key, CancellationToken cancellationToken = default)
        => LoadAsync(key, CacheKeyType, AllowCachePropagation);

    private Task<TValue?> LoadAsync(
        TKey key,
        string cacheKeyType,
        bool allowCachePropagation)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        PromiseCacheKey cacheKey = new(cacheKeyType, key);

        var promise = (Promise2<TValue>)_cache.GetOrAdd(
            cacheKey,
            static (_, cloned) => Promise2<TValue>.Create(cloned),
            !allowCachePropagation);

        if (!promise.TryInitialize(this, key, static (@this, key, p) => @this.InitializePromise(key, p)))
        {
            _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
        }

        return promise.Task;
    }

    private void InitializePromise(TKey key, Promise2<TValue> promise2)
    {
        var batch = _currentBatch;
        do
        {
            if (batch is not null && batch.TryAdd(key, promise2, _maxBatchSize))
            {
                return;
            }

            var newBatch = BatchPool2<TKey>.Shared.Get();
            var originalBatch = Interlocked.CompareExchange(ref _currentBatch, newBatch, batch);
            if (!ReferenceEquals(originalBatch, batch))
            {
                BatchPool2<TKey>.Shared.Return(newBatch);
                batch = originalBatch;
                continue;
            }

            _batchScheduler.Schedule(() => ExecuteBatch(newBatch));
            batch = newBatch;
        } while (true);
    }

    private async ValueTask ExecuteBatch(Batch2<TKey> batch)
    {
        Interlocked.CompareExchange(ref _currentBatch, null, batch);

        // TODO batch still can get new elements. Needs Execution lock which can be inspected, if started.
        var errors = false;

        using (_diagnosticEvents.ExecuteBatch(this, batch.Keys))
        {
            var buffer = new Result<TValue?>[batch.Keys.Count];

            try
            {
                var context = new DataLoaderFetchContext<TValue>(ContextData);
                await FetchAsync(batch.Keys, buffer, context, _ct).ConfigureAwait(false);
                BatchOperationSucceeded(batch, batch.Keys, buffer);
                _diagnosticEvents.BatchResults<TKey, TValue>(batch.Keys, buffer);
            }
            catch (Exception ex)
            {
                errors = true;
                BatchOperationFailed(batch, batch.Keys, ex);
            }
        }

        // we return the batch here so that the keys are only cleared
        // after the diagnostic events are done.
        if (!errors)
        {
            BatchPool2<TKey>.Shared.Return(batch);
        }
    }

    private void BatchOperationFailed(
        Batch2<TKey> batch,
        IReadOnlyList<TKey> keys,
        Exception error)
    {
        _diagnosticEvents.BatchError(keys, error);

        foreach (var key in keys)
        {
            if (_cache is not null)
            {
                PromiseCacheKey cacheKey = new(CacheKeyType, key);
                _cache.TryRemove(cacheKey, out _);
            }

            batch.GetPromise<TValue>(key).TrySetError(error);
        }
    }

    private void BatchOperationSucceeded(
        Batch2<TKey> batch,
        IReadOnlyList<TKey> keys,
        Result<TValue?>[] results)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var value = results[i];

            if (value.Kind is ResultKind.Undefined)
            {
                // in case we got here less or more results as expected, the
                // complete batch operation failed.
                Exception error = Errors.CreateKeysAndValuesMustMatch(keys.Count, i);
                BatchOperationFailed(batch, keys, error);
                return;
            }

            SetSingleResult(batch.GetPromise<TValue?>(key), key, value);
        }
    }
    private void SetSingleResult(
        Promise2<TValue?> promise,
        TKey key,
        Result<TValue?> result)
    {
        if (result.Kind is ResultKind.Value)
        {
            promise.TrySetResult(result);
        }
        else
        {
            _diagnosticEvents.BatchItemError(key, result.Error!);
            promise.TrySetError(result.Error!);
        }
    }

    public Task<IReadOnlyList<TValue?>> LoadAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Remove(TKey key)
    {
        throw new NotImplementedException();
    }

    public void Set(TKey key, Task<TValue?> value)
    {
        throw new NotImplementedException();
    }

#if NET8_0_OR_GREATER
    /// <inheritdoc />
    public ISelectionDataLoader<TKey, TValue> Branch(string key)
    {
        if(!AllowBranching)
        {
            throw new InvalidOperationException(
                "Branching is not allowed for this DataLoader.");
        }

        var branch = _branches.GetOrAdd(
            key,
            // TODO Fix SelectionDataLoader
            static (key, arg) => new SelectionDataLoader<TKey, TValue>((DataLoaderBase<TKey, TValue>)(object)arg, key),
            this);

        return branch;
    }
#endif

    /// <summary>
    /// A helper to create a cache key type for a DataLoader.
    /// </summary>
    /// <param name="type">
    /// The DataLoader type.
    /// </param>
    /// <returns>
    /// Returns the DataLoader cache key.
    /// </returns>
    protected static string GetCacheKeyType(Type type)
        => type.FullName ?? type.Name;
}

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Tasks;
using GreenDonut.Helpers;
#if NET8_0_OR_GREATER
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
    private readonly ConcurrentDictionary<PromiseCacheKey, IPromise>? _cache;
    private Batch2<TKey>? _currentBatch;

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

    public IImmutableDictionary<string, object?> ContextData { get; set; } =
        ImmutableDictionary<string, object?>.Empty;

    /// <inheritdoc />
    public Task<TValue?> LoadAsync(TKey key, CancellationToken cancellationToken = default)
        => LoadAsync(key, CacheKeyType, AllowCachePropagation, cancellationToken);

    private Task<TValue?> LoadAsync(
        TKey key,
        string cacheKeyType,
        bool allowCachePropagation,
        CancellationToken cancellationToken)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        PromiseCacheKey cacheKey = new(cacheKeyType, key);

        var promise = (Promise2<TValue?>)(_cache?.GetOrAdd(
            cacheKey,
            CreatePromise,
            allowCachePropagation) ?? CreatePromise(cacheKey, allowCachePropagation));

        if (!promise.TryInitialize(this, key, static (@this, key, p) => @this.InitializePromise(key, p)))
        {
            _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
        }

        return promise.Task;

        static Promise2<TValue?> CreatePromise(PromiseCacheKey key, bool allowCachePropagationLocal)
        {
            return Promise2<TValue?>.Create(!allowCachePropagationLocal);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TValue?>> LoadAsync(
        IReadOnlyCollection<TKey> keys,
        CancellationToken cancellationToken = default)
        => LoadAsync(keys, CacheKeyType, AllowCachePropagation, cancellationToken);

    private Task<IReadOnlyList<TValue?>> LoadAsync(
        IReadOnlyCollection<TKey> keys,
        string cacheKeyType,
        bool allowCachePropagation,
        CancellationToken cancellationToken)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }
        var tasks = new Task<TValue?>[keys.Count];
        var index = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            tasks[index++] = LoadAsync(key, cacheKeyType, allowCachePropagation, cancellationToken);
        }

        return WhenAll(tasks);

        static async Task<IReadOnlyList<TValue?>> WhenAll(Task<TValue?>[] tasks)
            => await Task.WhenAll(tasks).ConfigureAwait(false);

    }

    public void Remove(TKey key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (_cache is not null)
        {
            PromiseCacheKey cacheKey = new(CacheKeyType, key);
            _cache.TryRemove(cacheKey, out _);
        }
    }

    public void Set(TKey key, Task<TValue?> value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (_cache is not null)
        {
            PromiseCacheKey cacheKey = new(CacheKeyType, key);
            _cache.TryAdd(cacheKey, new Promise<TValue?>(value));
        }
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

    private void InitializePromise(TKey key, Promise2<TValue> promise2)
    {
        var batch = _currentBatch;
        do
        {
            if (batch is not null && batch.TryAdd(key, promise2, _maxBatchSize))
            {
                return;
            }
            // TODO Lock is better way here.
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
        //Remove the batch only if it is still the same.
        Interlocked.CompareExchange(ref _currentBatch, null, batch);

        var errors = false;

        // Before start processing, the batch must be frozen.
        batch.Close();
        var keys = batch.Keys;

        using (_diagnosticEvents.ExecuteBatch(this, keys))
        {
            var buffer = new Result<TValue?>[keys.Count];

            try
            {
                var context = new DataLoaderFetchContext<TValue>(ContextData);
                await FetchAsync(keys, buffer, context, _ct).ConfigureAwait(false);
                BatchOperationSucceeded(batch, keys, buffer);
                _diagnosticEvents.BatchResults<TKey, TValue>(keys, buffer);
            }
            catch (Exception ex)
            {
                errors = true;
                BatchOperationFailed(batch, keys, ex);
            }
        }

        // we return the batch here so that the keys are only cleared
        // after the diagnostic events are done.
        if (!errors)
        {
            BatchPool2<TKey>.Shared.Return(batch);
        }
    }

    private void BatchOperationFailed(Batch2<TKey> batch, IReadOnlyList<TKey> keys, Exception error)
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

    private void BatchOperationSucceeded(Batch2<TKey> batch, IReadOnlyList<TKey> keys, Result<TValue?>[] results)
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

    private void SetSingleResult(Promise2<TValue?> promise, TKey key, Result<TValue?> result)
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
}

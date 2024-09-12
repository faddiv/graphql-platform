using System.Collections.Immutable;
using GreenDonut.Helpers;

using static GreenDonut.NoopDataLoaderDiagnosticEventListener;

#if NET6_0_OR_GREATER
using GreenDonut.Projections;
#else
using GreenDonut.Helpers;
#endif

namespace GreenDonut;

public abstract partial class DataLoaderBase<TKey, TValue>
    : IDataLoader<TKey, TValue>
    where TKey : notnull
{
    private readonly IDataLoaderDiagnosticEvents _diagnosticEvents;
    private readonly CancellationToken _ct;
    private readonly IBatchScheduler _batchScheduler;
    private readonly int _maxBatchSize;
    private Batch<TKey>? _currentBatch;

#if NET6_0_OR_GREATER
    private ImmutableDictionary<string, IDataLoader> _branches =
        ImmutableDictionary<string, IDataLoader>.Empty;
    private Lock _branchesLock = new();
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
    protected DataLoaderBase(IBatchScheduler batchScheduler, DataLoaderOptions? options = null)
    {
        options ??= new DataLoaderOptions();
        _diagnosticEvents = options.DiagnosticEvents ?? NoopDataLoaderDiagnosticEventListener.Default;
        Cache = options.Cache;
        _ct = options.CancellationToken;
        _batchScheduler = batchScheduler;
        _maxBatchSize = options.MaxBatchSize;
        CacheKeyType = GetCacheKeyType(GetType());
    }

    /// <summary>
    /// Gets access to the cache of this DataLoader.
    /// </summary>
    protected internal IPromiseCache? Cache { get; }

    /// <summary>
    /// Gets the cache key type for this DataLoader.
    /// </summary>
    protected internal virtual string CacheKeyType { get; }

    /// <summary>
    /// Gets or sets the context data which can be used to store
    /// transient state on the DataLoader.
    /// </summary>
    public IImmutableDictionary<string, object?> ContextData { get; set; } =
        ImmutableDictionary<string, object?>.Empty;

    /// <summary>
    /// Specifies if the values fetched by this DataLoader
    /// are propagated through the cache.
    /// </summary>
    protected virtual bool AllowCachePropagation => true;

    /// <summary>
    /// Specifies if this DataLoader allows branching.
    /// </summary>
    protected virtual bool AllowBranching => true;

    /// <summary>
    /// Gets the batch scheduler of this DataLoader.
    /// </summary>
    protected internal IBatchScheduler BatchScheduler
        => _batchScheduler;

    /// <summary>
    /// Gets the options of this DataLoader.
    /// </summary>
    protected internal DataLoaderOptions Options
        => new()
        {
            MaxBatchSize = _maxBatchSize, Cache = Cache, DiagnosticEvents = _diagnosticEvents, CancellationToken = _ct,
        };

    /// <inheritdoc />
    public Task<TValue?> LoadAsync(TKey key, CancellationToken cancellationToken = default)
        => LoadAsync(key, CacheKeyType, AllowCachePropagation, true, cancellationToken);

    private Task<TValue?> LoadAsync(
        TKey key,
        string cacheKeyType,
        bool allowCachePropagation,
        bool scheduleOnNewBatch,
        CancellationToken cancellationToken)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        PromiseCacheKey cacheKey = new(cacheKeyType, key);

        var promise = Cache?.GetOrAddPromise(
            cacheKey,
            CreatePromise,
            allowCachePropagation) ?? CreatePromise(cacheKey, allowCachePropagation);

        if (!promise.TryInitialize(
            this, key, scheduleOnNewBatch,
            static (@this, key, s, p) => @this.InitializePromise(key, s, p)))
        {
            _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
        }

        return promise.Task;

        static Promise<TValue?> CreatePromise(PromiseCacheKey key, bool allowCachePropagationLocal)
        {
            return Promise<TValue?>.Create(!allowCachePropagationLocal);
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

            tasks[index++] = LoadAsync(key, cacheKeyType, allowCachePropagation, false, cancellationToken);
        }

        var batch = _currentBatch;
        if (batch is { IsScheduled: false  })
        {
            _batchScheduler.Schedule(() => ExecuteBatch(batch));
        }

        return WhenAll(tasks);

        static async Task<IReadOnlyList<TValue?>> WhenAll(Task<TValue?>[] tasks)
            => await Task.WhenAll(tasks).ConfigureAwait(false);

    }

    /// <inheritdoc />
    public void Remove(TKey key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (Cache is not null)
        {
            PromiseCacheKey cacheKey = new(CacheKeyType, key);
            Cache.TryRemove(cacheKey);
        }
    }

    /// <inheritdoc />
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

        if (Cache is not null)
        {
            PromiseCacheKey cacheKey = new(CacheKeyType, key);
            Cache.TryAdd(cacheKey, new Promise<TValue?>(value));
        }
    }

#if NET6_0_OR_GREATER
    /// <inheritdoc />
    public IDataLoader Branch<TState>(
        string key,
        CreateDataLoaderBranch<TKey, TValue, TState> createBranch,
        TState state)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(key));
        }

        if (createBranch == null)
        {
            throw new ArgumentNullException(nameof(createBranch));
        }

        if (!AllowBranching)
        {
            throw new InvalidOperationException(
                "Branching is not allowed for this DataLoader.");
        }

        if (!_branches.TryGetValue(key, out var branch))
        {
            lock (_branchesLock)
            {
                if (!_branches.TryGetValue(key, out branch))
                {
                    var newBranch = createBranch(key, this, state);
                    _branches = _branches.Add(key, newBranch);
                    return newBranch;
                }
            }
        }

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

    private void InitializePromise(TKey key, bool scheduleOnNewBatch, Promise<TValue> promise)
    {
        var batch = _currentBatch;
        do
        {
            if (batch is not null && batch.TryAdd(key, promise, _maxBatchSize))
            {
                return;
            }
            // TODO Lock is better way here.
            var newBatch = BatchPool<TKey>.Shared.Get();
            var originalBatch = Interlocked.CompareExchange(ref _currentBatch, newBatch, batch);
            if (!ReferenceEquals(originalBatch, batch))
            {
                // TODO Parallel execution runs here and slows down execution by a lot.
                BatchPool<TKey>.Shared.Return(newBatch);
                batch = originalBatch;
                continue;
            }

            if(scheduleOnNewBatch || !newBatch.IsScheduled)
            {
                _batchScheduler.Schedule(() => ExecuteBatch(newBatch));
            }
            batch = newBatch;
        } while (true);
    }

    private async ValueTask ExecuteBatch(Batch<TKey> batch)
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
            BatchPool<TKey>.Shared.Return(batch);
        }
    }

    private void BatchOperationFailed(Batch<TKey> batch, IReadOnlyList<TKey> keys, Exception error)
    {
        _diagnosticEvents.BatchError(keys, error);

        foreach (var key in keys)
        {
            if (Cache is not null)
            {
                PromiseCacheKey cacheKey = new(CacheKeyType, key);
                Cache.TryRemove(cacheKey);
            }

            batch.GetPromise<TValue>(key).TrySetError(error);
        }
    }

    private void BatchOperationSucceeded(Batch<TKey> batch, IReadOnlyList<TKey> keys, Result<TValue?>[] results)
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

    private void SetSingleResult(Promise<TValue?> promise, TKey key, Result<TValue?> result)
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

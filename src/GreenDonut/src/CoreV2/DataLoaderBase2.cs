using System.Buffers;
using System.Collections.Immutable;
using GreenDonut;
using GreenDonutV2.Internals;
using static GreenDonutV2.NoopDataLoaderDiagnosticEventListener;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
    : IDataLoader<TKey, TValue>,
        IDataLoaderNext
    where TKey : notnull
{
    private readonly IDataLoaderDiagnosticEvents _diagnosticEvents;
    private readonly IBatchScheduler _batchScheduler;
    private readonly int _maxBatchSize;
    private readonly Lock _batchExchangeLock = new();
    private Batch<TKey>? _currentBatch;

    private ImmutableDictionary<string, IDataLoader> _branches =
        ImmutableDictionary<string, IDataLoader>.Empty;

    private readonly Lock _branchesLock = new();

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
    protected DataLoaderBase2(IBatchScheduler batchScheduler, DataLoaderOptions2? options = null)
    {
        options ??= new DataLoaderOptions2();
        _diagnosticEvents = options.DiagnosticEvents ?? Default;
        _maxBatchSize = options.MaxBatchSize;
        Cache = options.Cache ?? new PromiseCache2(_maxBatchSize);
        _batchScheduler = batchScheduler;
        CacheKeyType = GetCacheKeyType(GetType());
    }

    /// <summary>
    /// Gets access to the cache of this DataLoader.
    /// </summary>
    protected internal IPromiseCache2? Cache { get; }

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
        => new() { MaxBatchSize = _maxBatchSize, Cache = Cache, DiagnosticEvents = _diagnosticEvents, };

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

    public void RemoveCacheEntry(TKey key)
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

    public void SetCacheEntry(TKey key, Task<TValue?> value)
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

        if (_branches.TryGetValue(key, out var branch))
        {
            return branch;
        }

        lock (_branchesLock)
        {
            if (_branches.TryGetValue(key, out branch))
            {
                return branch;
            }

            var newBranch = createBranch(key, this, state);
            _branches = _branches.Add(key, newBranch);
            return newBranch;
        }
    }

    /// <summary>
    /// A helper to create a cache key type for a DataLoader.
    /// </summary>
    /// <typeparam name="TDataLoader">The DataLoader type.</typeparam>
    /// <returns>
    /// Returns the DataLoader cache key.
    /// </returns>
    // ReSharper disable once UnusedMember.Global
    protected static string GetCacheKeyType<TDataLoader>()
        where TDataLoader : IDataLoader
        => GetCacheKeyType(typeof(TDataLoader));

    /// <summary>
    /// A helper to create a cache key type for a DataLoader.
    /// </summary>
    /// <param name="type">
    /// The DataLoader type.
    /// </param>
    /// <returns>
    /// Returns the DataLoader cache key.
    /// </returns>
    // ReSharper disable once MemberCanBePrivate.Global
    protected static string GetCacheKeyType(Type type)
        => type.FullName ?? type.Name;

    private Batch<TKey> CreateNewBatch(Batch<TKey>? oldBatch)
    {
        var newBatch = _currentBatch;
        if (newBatch is not null && newBatch != oldBatch)
        {
            return newBatch;
        }

        lock (_batchExchangeLock)
        {
            newBatch = _currentBatch;
            if (newBatch is not null && newBatch != oldBatch)
            {
                return newBatch;
            }

            newBatch = BatchPool<TKey>.Shared.Get();
            newBatch.MaxSize = _maxBatchSize;
            _currentBatch = newBatch;
            return newBatch;
        }
    }

    private void EnsureBatchExecuted(Batch<TKey>? batch, CancellationToken cancellationToken)
    {
        if (batch?.NeedsScheduling() ?? false)
        {
            ExecuteBatchInternal(batch, cancellationToken);
        }
    }

    private void ExecuteBatchInternal(Batch<TKey> batch, CancellationToken cancellationToken)
    {
        _batchScheduler.Schedule(() => ExecuteBatch(batch, cancellationToken));
    }

    private async ValueTask ExecuteBatch(Batch<TKey> batch, CancellationToken cancellationToken)
    {
        //Remove the batch only if it is still the same.
        Interlocked.CompareExchange(ref _currentBatch, null, batch);

        batch.Close();

        var errors = false;

        var keys = batch.Keys;

        using (_diagnosticEvents.ExecuteBatch(this, keys))
        {
            var array = ArrayPool<Result<TValue?>>.Shared.Rent(keys.Count);
            var buffer = array.AsMemory(0, keys.Count);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new DataLoaderFetchContext<TValue>(ContextData);
                await FetchAsync(keys, buffer, context, cancellationToken).ConfigureAwait(false);
                BatchOperationSucceeded(batch, keys, buffer.Span);
            }
            catch (Exception ex)
            {
                errors = true;
                BatchOperationFailed(batch, keys, ex);
            }
            finally
            {
                ArrayPool<Result<TValue?>>.Shared.Return(array, clearArray: true);
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

    private void BatchOperationSucceeded(
        Batch<TKey> batch,
        IReadOnlyList<TKey> keys,
        ReadOnlySpan<Result<TValue?>> results)
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

        _diagnosticEvents.BatchResults(keys, results);
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
            if (Cache.TryGetOrAddPromise(cacheKey,
                static (_, state) => state.@this.CreatePromiseFromBatch(state.key, state.cancellationToken),
                (@this: this, key, cancellationToken), out promise))
            {
                _diagnosticEvents.ResolvedTaskFromCache(this, cacheKey, promise.Task);
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
                out Promise<TValue?>? promise))
            {
                return promise.Value;
            }

            EnsureBatchExecuted(currentBranch, cancellationToken);
            currentBranch = CreateNewBatch(currentBranch);
        }
    }
}
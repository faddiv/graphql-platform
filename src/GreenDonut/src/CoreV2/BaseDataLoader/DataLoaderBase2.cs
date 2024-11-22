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

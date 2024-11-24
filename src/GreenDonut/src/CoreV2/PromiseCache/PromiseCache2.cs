using System.Collections.Concurrent;
using GreenDonut;
using GreenDonutV2.Internals;

namespace GreenDonutV2;

/// <summary>
/// A memorization cache for <c>DataLoader</c>.
/// </summary>
/// <remarks>
/// Creates a new instance of <see cref="PromiseCache2"/>.
/// </remarks>
/// <param name="size">
/// The size of the cache. The minimum cache size is 10.
/// </param>
public sealed partial class PromiseCache2(int size) : IPromiseCache2
{
    private readonly ConcurrentDictionary<PromiseCacheKey, IPromise> _promises = new();
    private readonly ConcurrentDictionary<Type, ConcurrentStack<Subscription>> _subscriptions = new();
    private readonly ConcurrentStack<IPromise> _promises2 = new();
    private readonly int _size = InternalHelpers.CalculateSize(size);
    private int _usage;

    /// <inheritdoc />
    public int Size => _size;

    /// <inheritdoc />
    public int Usage => _usage;

    public bool TryGetOrAddPromise<T, TState>(
        PromiseCacheKey key,
        Func<PromiseCacheKey, TState, Promise<T>> createPromise,
        TState state,
        out Promise<T> promise)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        if (_promises.TryGetValue(key, out var entry))
        {
            promise = entry.As<T>();
            return true;
        }

        promise = createPromise(key, state);
        if (!TryReserveSlot())
        {
            return false;
        }

        var createdPromise = _promises.GetOrAdd(key, promise);
        if (!ReferenceEquals(promise.Task, createdPromise.Task))
        {
            Interlocked.Decrement(ref _usage);
            promise = createdPromise.As<T>();
            return true;
        }

        return false;
    }

    public Task<T> GetOrAddTask<T>(PromiseCacheKey key, Func<PromiseCacheKey, Promise<T>> createPromise)
    {
        TryGetOrAddPromise(
            key,
            static (key, p) => p(key),
            createPromise,
            out var promise);
        return promise.Task;
    }

    /// <inheritdoc />
    public bool TryAdd<T>(PromiseCacheKey key, Promise<T> promise)
    {
        if (key.Type is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (promise.Task is null)
        {
            throw new ArgumentNullException(nameof(promise));
        }

        return TryAddInternal(key, static args => args, promise);
    }

    /// <inheritdoc />
    public bool TryAdd<T>(PromiseCacheKey key, Func<Promise<T>> createPromise)
    {
        if (key.Type is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (createPromise is null)
        {
            throw new ArgumentNullException(nameof(createPromise));
        }

        return TryAddInternal(key, static args => args(), createPromise);
    }

    /// <inheritdoc />
    public bool TryRemove(PromiseCacheKey key)
    {
        if (!_promises.TryRemove(key, out _))
        {
            return false;
        }

        Interlocked.Decrement(ref _usage);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        // ReSharper disable once InconsistentlySynchronizedField
        _promises.Clear();
        _promises2.Clear();
        _subscriptions.Clear();
        _usage = 0;
    }

    private bool TryAddInternal<T, TState>(
        PromiseCacheKey key,
        Func<TState, Promise<T>> createPromise,
        TState state)
    {
        if (!TryReserveSlot())
        {
            return false;
        }

        var promise = createPromise(state);
        var createdPromise = _promises.GetOrAdd(key, promise);
        if (!ReferenceEquals(promise.Task, createdPromise.Task))
        {
            Interlocked.Decrement(ref _usage);
            return false;
        }

        NotifySubscribersOnComplete(promise, key);
        return true;
    }

    private bool TryReserveSlot()
    {
        if (_usage >= _size)
        {
            return false;
        }

        var usage = Interlocked.Increment(ref _usage);
        // ReSharper disable once InvertIf
        if (usage > _size)
        {
            Interlocked.Decrement(ref _usage);
            return false;
        }

        return true;
    }
}

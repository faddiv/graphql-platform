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
    private readonly int _lockThreshold = InternalHelpers.CalculateLockThreshold(size);

    private readonly Lock _mutationLock = new();
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
        if (_usage >= _size)
        {
            return false;
        }

        if (_usage >= _lockThreshold)
        {
            lock (_mutationLock)
            {
                if (_usage >= _size)
                {
                    return false;
                }

                if (TryAddNoLockInternal(key, promise))
                {
                    return false;
                }
            }
        }
        else
        {
            if (TryAddNoLockInternal(key, promise))
            {
                return false;
            }
        }


        // ReSharper disable once InconsistentlySynchronizedField
        if (_promises.TryGetValue(key, out entry))
        {
            promise = entry.As<T>();
            return false;
        }

        throw new InvalidOperationException($"Could not get or add Promise with key: {key}");
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
        lock (_mutationLock)
        {
            if (!_promises.TryRemove(key, out _))
            {
                return false;
            }

            Interlocked.Decrement(ref _usage);
            return true;
        }
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
        if (_usage >= _size)
        {
            return false;
        }

        if (_usage >= _lockThreshold)
        {
            lock (_mutationLock)
            {
                if (_usage >= _size)
                {
                    return false;
                }

                var promise = createPromise(state);
                if (TryAddNoLockInternal(key, promise))
                {
                    return true;
                }
            }
        }
        else
        {
            if (TryAddNoLockInternal(key, createPromise(state)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryAddNoLockInternal<T>(PromiseCacheKey key, Promise<T> promise)
    {
        if (!_promises.TryAdd(key, promise))
        {
            return false;
        }

        Interlocked.Increment(ref _usage);
        NotifySubscribersOnComplete(promise, key);
        return true;
    }
}

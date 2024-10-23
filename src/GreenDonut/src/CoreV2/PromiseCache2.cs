using System.Buffers;
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
public sealed class PromiseCache2(int size) : IPromiseCache2
{
    private readonly ConcurrentDictionary<PromiseCacheKey, Entry> _promises = new();
    private readonly ConcurrentDictionary<Type, ConcurrentStack<Subscription>> _subscriptions = new();
    private readonly ConcurrentStack<IPromise> _promises2 = new();
    private readonly int _size = InternalHelpers.CalculateSize(size);
    private readonly int _lockThreshold = InternalHelpers.CalculateLockThreshold(size);

    private readonly Lock _mutationLock = new();
    private int _usage;
    private bool _enableSubscriptions;

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
            promise = entry.Promise.As<T>();
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
            promise = entry.Promise.As<T>();
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
    public void Publish<T>(T value)
    {
        var promise = Promise<T>.Create(value, cloned: true);

        Interlocked.Increment(ref _usage);
        _promises2.Push(promise);

        if (!_enableSubscriptions ||
            !_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        foreach (var subscription in subscriptions)
        {
            if (subscription is Subscription<T> casted)
            {
                casted.OnNext(promise);
            }
        }
    }

    /// <inheritdoc />
    public void PublishMany<T>(ReadOnlySpan<T> values)
    {
        if (values.Length == 0 || !_enableSubscriptions)
        {
            return;
        }

        var buffer = ArrayPool<IPromise>.Shared.Rent(values.Length);
        try
        {
            var span = buffer.AsSpan()[..values.Length];

            for (var i = 0; i < values.Length; i++)
            {
                span[i] = Promise<T>.Create(values[i], cloned: true);
            }

            Interlocked.Add(ref _usage, values.Length);
            _promises2.PushRange(buffer, 0, values.Length);

            // now we notify all subscribers that are interested in the current promise type.
            if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
            {
                return;
            }

            foreach (var subscription in subscriptions)
            {
                if (subscription is not Subscription<T> casted)
                {
                    continue;
                }

                foreach (var item in span)
                {
                    casted.OnNext((Promise<T>)item);
                }
            }
        }
        finally
        {
            ArrayPool<IPromise>.Shared.Return(buffer, true);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Action<IPromiseCache, Promise<T>> next, string? skipCacheKeyType)
    {
        _enableSubscriptions = true;
        var subscriptions = _subscriptions.GetOrAdd(typeof(T), _ => []);
        var subscription = new Subscription<T>(this, next, skipCacheKeyType);

        subscriptions.Push(subscription);

        if (!_promises2.IsEmpty)
        {
            foreach (var promise in _promises2)
            {
                if (promise is Promise<T> casted)
                {
                    subscription.OnNext(casted);
                }
            }
        }

        foreach (var (key, entry) in _promises)
        {
            if (entry.Promise is not Promise<T> promise)
            {
                continue;
            }

            if (subscription.TryOnNext(key, promise))
            {
                continue;
            }

            CreateSubscription(entry, promise, key);
        }

        return subscription;
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
        Interlocked.Increment(ref _usage);
        var entry = new Entry { Promise = promise };
        if (!_promises.TryAdd(key, entry))
        {
            Interlocked.Decrement(ref _usage);
            return false;
        }

        NotifySubscribersOnComplete(entry, promise, key);
        return true;
    }

    private void NotifySubscribersOnComplete<TValue>(Entry entry, Promise<TValue> promise, PromiseCacheKey key)
    {
        if (promise.IsClone)
        {
            throw new InvalidCastException(
                "The promise is a clone and cannot be used to register a callback.");
        }

        if (!_enableSubscriptions)
        {
            return;
        }

        CreateSubscription(entry, promise, key);
    }

    private void CreateSubscription<TValue>(Entry entry, Promise<TValue> promise, PromiseCacheKey key)
    {
        lock (entry)
        {
            if (entry.SubscriptionHandler is not null)
            {
                return;
            }

            void ContinuationFunction(Task<TValue> task)
            {
                if (task is { IsCompletedSuccessfully: true, Result: not null })
                {
                    NotifySubscribers(key, promise);
                }
            }

            var c = ContinuationFunction;

            entry.SubscriptionHandler = c;
            promise.Task.ContinueWith(c, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private void NotifySubscribers<T>(in PromiseCacheKey key, in Promise<T> promise)
    {
        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        var clonedPromise = promise.Clone();

        foreach (var subscription in subscriptions)
        {
            if (subscription is Subscription<T> casted)
            {
                casted.TryOnNext(key, clonedPromise);
            }
        }
    }

    private sealed class Subscription<T>(
        PromiseCache2 owner,
        Action<IPromiseCache, Promise<T>> next,
        string? skipCacheKeyType) : Subscription
    {
        public bool TryOnNext(PromiseCacheKey key, Promise<T> promise)
        {
            if (Disposed)
            {
                return true;
            }

            if (promise.Task is { IsCompletedSuccessfully: true, Result: not null } &&
                skipCacheKeyType?.Equals(key.Type, StringComparison.Ordinal) != true)
            {
                next(owner, promise);
                return true;
            }

            return false;
        }

        public void OnNext(Promise<T> promise)
        {
            if (Disposed)
            {
                return;
            }

            if (promise.Task.IsCompletedSuccessfully)
            {
                next(owner, promise);
            }
        }
    }

    private abstract class Subscription
        : IDisposable
    {
        protected bool Disposed { get; private set; }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
        }
    }

    private class Entry
    {
        public required IPromise Promise { get; init; }
        public object? SubscriptionHandler { get; set; }
    }
}

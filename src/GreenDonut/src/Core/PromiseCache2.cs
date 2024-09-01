using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using GreenDonut.Helpers;

namespace GreenDonut;

/// <summary>
/// A memorization cache for <c>DataLoader</c>.
/// </summary>
public sealed class PromiseCache2 : IPromiseCache
{
    private const int _minimumSize = 10;
    private readonly ConcurrentDictionary<PromiseCacheKey, Entry> _promises = new();
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly ConcurrentStack<IPromise> _promises2 = new();
    private readonly int _size;
    private readonly int _order;
    private int _usage;

    /// <summary>
    /// Creates a new instance of <see cref="PromiseCache"/>.
    /// </summary>
    /// <param name="size">
    /// The size of the cache. The minimum cache size is 10.
    /// </param>
    public PromiseCache2(int size)
    {
        _size = size < _minimumSize ? _minimumSize : size;
        _order = Convert.ToInt32(size * 0.9);
    }

    /// <inheritdoc />
    public int Size => _size;

    /// <inheritdoc />
    public int Usage => _usage;

    public Task<T> GetOrAddTask<T>(PromiseCacheKey key, Func<PromiseCacheKey, Promise<T>> createPromise)
    {
        if (key.Type is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (createPromise is null)
        {
            throw new ArgumentNullException(nameof(createPromise));
        }

        var result = GetOrAddEntryInternal(key, static (key, args) => args(key), createPromise);

        return result.promise.Task;
    }

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

        var result = GetOrAddEntryInternal(key, static (_, args) => args, promise);

        return result.newEntry;
    }

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

        var result = GetOrAddEntryInternal(key, static (_, args) => args(), createPromise);

        return result.newEntry;
    }

    public bool TryRemove(PromiseCacheKey key)
    {
        return _promises.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public void Publish<T>(T value)
    {
        var promise = Promise<T>.Create(value, cloned: true);

        _promises2.Push(promise);

        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        List<Subscription> clone;
        lock (subscriptions)
        {
            clone = subscriptions.ToList();
        }
        foreach (var subscription in clone)
        {
            if (subscription is Subscription<T> casted)
            {
                casted.OnNext(promise);
            }
        }
    }


    public void PublishMany<T>(IReadOnlyList<T> values)
    {
        var buffer = ArrayPool<IPromise>.Shared.Rent(values.Count);
        var span = buffer.AsSpan().Slice(0, values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var promise = Promise<T>.Create(values[i], cloned: true);
            span[i] = promise;
        }

        _promises2.PushRange(buffer, 0, values.Count);

        if (_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            List<Subscription> clone;
            lock (subscriptions)
            {
                clone = subscriptions.ToList();
            }
            foreach (var subscription in clone)
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

        span.Clear();
        ArrayPool<IPromise>.Shared.Return(buffer);
    }

    public IDisposable Subscribe<T>(Action<IPromiseCache, Promise<T>> next, string? skipCacheKeyType)
    {
        var type = typeof(T);
        var p1 = _promises2.ToArray();
        var p2 = _promises.ToArray();
        var subscriptions =  _subscriptions.GetOrAdd(type, _ => []);
        var subscription = new Subscription<T>(this, subscriptions, next, skipCacheKeyType);

        lock (subscriptions)
        {
            subscriptions.Add(subscription);
        }

        foreach (var promise in p1.OfType<Promise<T>>())
        {
            subscription.OnNext(promise);
        }

        foreach (var keyValuePair in p2)
        {
            if (keyValuePair.Value.Promise is Promise<T> promise)
            {
                subscription.OnNext(keyValuePair.Key, promise);
            }
        }

        return subscription;
    }

    public void Clear()
    {
        _promises.Clear();
        _promises2.Clear();
        _subscriptions.Clear();
        _usage = 0;
    }

    private (bool newEntry, Promise<T> promise) GetOrAddEntryInternal<T, TState>(
        PromiseCacheKey key,
        Func<PromiseCacheKey, TState, Promise<T>> createPromise,
        TState state)
    {
        if (_usage > _order && _usage >= _size)
        {
            var nonCachedEntry = new Entry(key, createPromise(key, state));
            return nonCachedEntry.EnsureInitialized<T>(this);
        }

#if NET6_0_OR_GREATER
        var entry = _promises.GetOrAdd(
            key,
            static (k, args) => new Entry(k, args.createPromise(k, args.state)),
            (createPromise, state));
#else
        var entry = _promises.GetOrAdd(
            key,
            k => new Entry(k, createPromise(k, state)));
#endif

        return entry.EnsureInitialized<T>(this);
    }

    private static void NotifySubscribers<T>(Promise<T> promise, CacheAndKey state)
        => state.Cache.NotifySubscribers(state.Key, promise);

    private void NotifySubscribers<T>(PromiseCacheKey key, Promise<T> promise)
    {
        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        promise = promise.Clone();

        List<Subscription> clone;
        lock (subscriptions)
        {
            clone = subscriptions.ToList();
        }

        foreach (var subscription in clone)
        {
            if (subscription is Subscription<T> casted)
            {
                casted.OnNext(key, promise);
            }
        }
    }

    private class Entry(PromiseCacheKey key, IPromise promise)
    {
        private volatile bool _initialized;
        private readonly object _lock = new();
        public PromiseCacheKey Key { get; } = key;
        public IPromise Promise { get; } = promise;

        public (bool newEntry, Promise<T> promise) EnsureInitialized<T>(PromiseCache2 cache)
        {
            if (Promise is not Promise<T> promise)
            {
                throw new InvalidOperationException(
                    $"Promise is not type of {typeof(Promise<T>).FullName}. Real type {Promise.GetType().FullName}");
            }

            if (_initialized)
            {
                return (false, promise);
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return (false, promise);
                }

                if (!promise.IsClone)
                {
                    promise.OnComplete(NotifySubscribers, new CacheAndKey(cache, key));
                }

                _initialized = true;
            }

            return (true, promise);
        }
    }

    private sealed class Subscription<T>(
        IPromiseCache owner,
        List<Subscription> subscriptions,
        Action<IPromiseCache, Promise<T>> next,
        string? skipCacheKeyType)
        : Subscription(typeof(T), subscriptions)
    {
        public void OnNext(PromiseCacheKey key, Promise<T> promise)
        {
            if (promise.Task.IsCompletedSuccessfully() &&
                skipCacheKeyType?.Equals(key.Type, StringComparison.Ordinal) != true)
            {
                next(owner, promise);
            }
        }

        public void OnNext(Promise<T> promise)
        {
            if (promise.Task.IsCompletedSuccessfully())
            {
                next(owner, promise);
            }
        }
    }

    private abstract class Subscription(
        Type type,
        List<Subscription> subscriptions)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (subscriptions)
            {
                subscriptions.Remove(this);
            }

            _disposed = true;
        }
    }

    private readonly struct CacheAndKey(PromiseCache2 cache, PromiseCacheKey key)
    {
        public PromiseCache2 Cache { get; } = cache;

        public PromiseCacheKey Key { get; } = key;
    }
}

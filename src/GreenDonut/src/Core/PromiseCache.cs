using System.Buffers;
using System.Collections.Concurrent;
using GreenDonut.Helpers;

namespace GreenDonut;

/// <summary>
/// A memorization cache for <c>DataLoader</c>.
/// </summary>
/// <remarks>
/// Creates a new instance of <see cref="PromiseCache"/>.
/// </remarks>
/// <param name="size">
/// The size of the cache. The minimum cache size is 10.
/// </param>
public sealed class PromiseCache(int size) : IPromiseCache
{
    private const int _minimumSize = 10;
    private readonly ConcurrentDictionary<PromiseCacheKey, Entry> _promises = new();
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly ConcurrentStack<IPromise> _promises2 = new();
    private readonly int _size = Math.Max(size, _minimumSize);
    private int _usage;

    /// <inheritdoc />
    public int Size => _size;

    /// <inheritdoc />
    public int Usage => _usage;

    /// <inheritdoc />
    public Promise<T> GetOrAddPromise<T, TState>(
        PromiseCacheKey key,
        Func<PromiseCacheKey, TState, Promise<T>> createPromise,
        TState state)
    {
        if (key.Type is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ArgumentNullException.ThrowIfNull(createPromise);

        var result = GetOrAddEntryInternal(key, createPromise, state);

        return result.promise;
    }

    /// <inheritdoc />
    public bool TryAdd<T>(PromiseCacheKey key, Promise<T> promise)
    {
        if (key.Type is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (promise?.Task is null)
        {
            throw new ArgumentNullException(nameof(promise));
        }

        var result = GetOrAddEntryInternal(key, static (_, args) => args, promise);

        return result.newEntry;
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

        var result = GetOrAddEntryInternal(key, static (_, args) => args(), createPromise);

        return result.newEntry;
    }

    /// <inheritdoc />
    public bool TryRemove(PromiseCacheKey key)
    {
        if (!_promises.TryRemove(key, out _))
        {
            return false;
        }

        IncrementInternal(-1);
        return true;
    }

    /// <inheritdoc />
    public void Publish<T>(T value)
    {
        var promise = Promise<T>.Create(value, cloned: true);

        _promises2.Push(promise);
        IncrementInternal();

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

    /// <inheritdoc />
    public void PublishMany<T>(ReadOnlySpan<T> values)
    {
        var buffer = ArrayPool<IPromise>.Shared.Rent(values.Length);
        try
        {
            var span = buffer.AsSpan()[..values.Length];

            for (var i = 0; i < values.Length; i++)
            {
                var promise = Promise<T>.Create(values[i], cloned: true);
                span[i] = promise;
            }

            _promises2.PushRange(buffer, 0, values.Length);
            IncrementInternal(values.Length);

            // now we notify all subscribers that are interested in the current promise type.
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
        var type = typeof(T);
        var p1 = _promises2.ToArray();
        var p2 = _promises.ToArray();
        var subscriptions = _subscriptions.GetOrAdd(type, _ => []);
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

    /// <inheritdoc />
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
        var usage = _usage;
        if (usage >= _size)
        {
            var nonCachedEntry = new Entry(key, createPromise(key, state));
            return nonCachedEntry.EnsureInitialized<T>(this, false);
        }

        var entry = _promises.GetOrAdd(
            key,
            static (k, args) => new Entry(k, args.createPromise(k, args.state)),
            (createPromise, state));

        return entry.EnsureInitialized<T>(this, true);
    }

    internal void NotifySubscribers<T>(in PromiseCacheKey key, in Promise<T> promise)
    {
        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        var clonedPromise = promise.Clone();

        Span<Subscription> clone = default;
        Subscription[]? array = null;
        try
        {
            lock (subscriptions)
            {
                if (subscriptions.Count == 0)
                {
                    return;
                }
                else
                {
                    array = ArrayPool<Subscription>.Shared.Rent(subscriptions.Count);
                    clone = array.AsSpan(0, subscriptions.Count);
                    subscriptions.CopyTo(clone);
                }
            }

            foreach (var subscription in clone)
            {
                if (subscription is Subscription<T> casted)
                {
                    casted.OnNext(key, clonedPromise);
                }
            }
        }
        finally
        {
            if (array is not null)
            {
                ArrayPool<Subscription>.Shared.Return(array, true);
            }
        }
    }

    private void IncrementInternal(int value = 1)
    {
        Interlocked.Add(ref _usage, value);
    }

    private class Entry(PromiseCacheKey key, IPromise promise)
    {
        private bool _initialized;
        private readonly Lock _lock = new();
        public PromiseCacheKey Key { get; } = key;
        public IPromise Promise { get; } = promise;

        public (bool newEntry, Promise<T> promise) EnsureInitialized<T>(PromiseCache cache, bool incrementUsage)
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

            bool notifySubscribers = false;
            lock (_lock)
            {
                if (_initialized)
                {
                    return (false, promise);
                }

                if (!promise.IsClone)
                {
                    notifySubscribers = true;
                }

                if (incrementUsage)
                {
                    cache.IncrementInternal();
                }

                _initialized = true;
            }

            if (notifySubscribers)
            {
                promise.NotifySubscribersOnComplete(cache, Key);
            }

            return (true, promise);
        }
    }

    private sealed class Subscription<T>(
        IPromiseCache owner,
        List<Subscription> subscriptions,
        Action<IPromiseCache, Promise<T>> next,
        string? skipCacheKeyType)
        : Subscription(subscriptions)
    {
        public void OnNext(PromiseCacheKey key, Promise<T> promise)
        {
            if (promise.Task.IsCompletedSuccessfully &&
                skipCacheKeyType?.Equals(key.Type, StringComparison.Ordinal) != true)
            {
                next(owner, promise);
            }
        }

        public void OnNext(Promise<T> promise)
        {
            if (promise.Task.IsCompletedSuccessfully)
            {
                next(owner, promise);
            }
        }
    }

    private abstract class Subscription(
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
}

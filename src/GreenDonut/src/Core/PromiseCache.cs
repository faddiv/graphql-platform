using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GreenDonut.Internals;

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
    private readonly ConcurrentDictionary<PromiseCacheKey, IPromise> _promises = new();
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly ConcurrentStack<IPromise> _promises2 = new();
    private readonly int _size = Math.Max(size, _minimumSize);
    private Lock _mutationLock = new();
    private int _usage;

    /// <inheritdoc />
    public int Size => _size;

    /// <inheritdoc />
    public int Usage => _usage;

    public bool TryGetOrAddPromise<T, TState>(PromiseCacheKey key, Func<PromiseCacheKey, TState, Promise<T>> createPromise, TState state, out Promise<T> promise)
    {
        if (_promises.TryGetValue(key, out var entry))
        {
            promise = entry.As<T>();
            return true;
        }

        if (TryAddInternal(key, createPromise, state, out var p))
        {
            promise = p;
            return false;
        }

        if (_promises.TryGetValue(key, out entry))
        {
            promise = entry.As<T>();
            return false;
        }

        throw new InvalidOperationException($"Could not get or add Promise with key: {key}");
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

        return TryAddInternal(key, static (_, args) => args, promise, out _);
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

        return TryAddInternal(key, static (_, args) => args(), createPromise, out _);
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

        _promises2.Push(promise);
        Interlocked.Increment(ref _usage);

        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        Subscription[]? array = null;
        Span<Subscription> clone = default;
        try
        {
            lock (subscriptions)
            {
                var count = subscriptions.Count;
                if (count == 0)
                {
                    return;
                }
                else if (count > 16)
                {
                    array = ArrayPool<Subscription>.Shared.Rent(count);
                    clone = array.AsSpan(0, count);
                }
                else
                {
                    var stack = new StackArray16<Subscription>();
                    clone = MemoryMarshal.CreateSpan(ref stack.first!, count);
                }
                subscriptions.CopyTo(clone);
            }
            foreach (var subscription in clone)
            {
                if (subscription is Subscription<T> casted)
                {
                    casted.OnNext(promise);
                }
            }
        }
        finally
        {
            if (array != null)
            {
                ArrayPool<Subscription>.Shared.Return(array);
            }
        }
    }

    /// <inheritdoc />
    public void PublishMany<T>(ReadOnlySpan<T> values)
    {
        if(values.Length == 0)
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

            _promises2.PushRange(buffer, 0, values.Length);
            Interlocked.Add(ref _usage, values.Length);

            // now we notify all subscribers that are interested in the current promise type.
            if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
            {
                return;
            }

            Span<Subscription> clone = default;
            Subscription[]? array = null;
            try
            {
                lock (subscriptions)
                {
                    var count = subscriptions.Count;
                    if (count == 0)
                    {
                        return;
                    }
                    else if (count > 16)
                    {
                        array = ArrayPool<Subscription>.Shared.Rent(count);
                        clone = array.AsSpan(0, count);
                    }
                    else
                    {
                        var stack = new StackArray16<Subscription>();
                        clone = MemoryMarshal.CreateSpan(ref stack.first!, count);
                    }
                    subscriptions.CopyTo(clone);
                }

                foreach (var subscription in clone)
                {
                    if (subscription is Subscription<T> casted)
                    {
                        foreach (var item in span)
                        {
                            casted.OnNext((Promise<T>)item);
                        }
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
        finally
        {
            ArrayPool<IPromise>.Shared.Return(buffer, true);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Action<IPromiseCache, Promise<T>> next, string? skipCacheKeyType)
    {
        var subscriptions = _subscriptions.GetOrAdd(typeof(T), _ => []);
        var subscription = new Subscription<T>(this, next, skipCacheKeyType);

        lock (subscriptions)
        {
            subscriptions.Add(subscription);
        }

        if (!_promises2.IsEmpty)
        {
            foreach (var promise in _promises2.OfType<Promise<T>>())
            {
                subscription.OnNext(promise);
            }
        }

        var promisesWithKey = _promises.ToArray();
        foreach (var keyValuePair in promisesWithKey)
        {
            if (keyValuePair.Value is Promise<T> promise)
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

    private bool TryAddInternal<T, TState>(
        PromiseCacheKey key,
        Func<PromiseCacheKey, TState, Promise<T>> createPromise,
        TState state,
        [NotNullWhen(true)]out Promise<T>? promise)
    {
        if (_usage >= _size)
        {
            promise = null;
            return false;
        }

        lock (_mutationLock)
        {
            if (_usage >= _size)
            {
                promise = null;
                return false;
            }

            promise = createPromise(key, state);
            if (_promises.TryAdd(key, promise))
            {
                Interlocked.Increment(ref _usage);
                promise.NotifySubscribersOnComplete(this, key);
                return true;
            }
        }

        return false;
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
                var count = subscriptions.Count;
                if (count == 0)
                {
                    return;
                }
                else if (count > 16)
                {
                    array = ArrayPool<Subscription>.Shared.Rent(count);
                    clone = array.AsSpan(0, count);
                }
                else
                {
                    var stack = new StackArray16<Subscription>();
                    clone = MemoryMarshal.CreateSpan(ref stack.first!, count);
                }
                subscriptions.CopyTo(clone);
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

    private sealed class Subscription<T>(
        PromiseCache owner,
        Action<IPromiseCache, Promise<T>> next,
        string? skipCacheKeyType) : Subscription
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

        protected override void Unsubscribe()
        {
            owner.Unsubscribe(this);
        }
    }

    private abstract class Subscription
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Unsubscribe();


            _disposed = true;
        }

        protected abstract void Unsubscribe();
    }

    private void Unsubscribe<T>(Subscription<T> subscription)
    {
        if (_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            lock (subscriptions)
            {
                subscriptions.Remove(subscription);
            }
        }
    }
}

using System.Buffers;
using GreenDonut;
using GreenDonutV2.Internals;

namespace GreenDonutV2;

partial class PromiseCache2
{
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
        if (values.Length == 0)
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

            foreach (var subscription in subscriptions)
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
            ArrayPool<IPromise>.Shared.Return(buffer, true);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Action<IPromiseCache, Promise<T>> next, string? skipCacheKeyType)
    {
        var subscriptions = _subscriptions.GetOrAdd(typeof(T), _ => []);
        var subscription = new Subscription<T>(this, next, skipCacheKeyType);

        //TODO Subscription check in NotifySubscribers little bit expensive. Can it enable only if something subscribe? Is there always a subscriber?
        lock (subscriptions)
        {
            subscriptions.Push(subscription);
        }

        if (!_promises2.IsEmpty)
        {
            foreach (var promise in _promises2.OfType<Promise<T>>())
            {
                subscription.OnNext(promise);
            }
        }

        foreach (var keyValuePair in _promises)
        {
            if (keyValuePair.Value is Promise<T> promise)
            {
                subscription.OnNext(keyValuePair.Key, promise);
            }
        }

        return subscription;
    }

    private void NotifySubscribersOnComplete<TValue>(Promise<TValue> promise, PromiseCacheKey key)
    {
        if (promise.IsClone)
        {
            throw new InvalidCastException(
                "The promise is a clone and cannot be used to register a callback.");
        }

        promise.Task.ContinueWith(static (_, o) =>
            {
                var (owner, key, promise) = ((PromiseCache2, PromiseCacheKey, Promise<TValue>))o!;
                owner.NotifySubscribers(key, promise);
            }, (this, key, promise),
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private void NotifySubscribers<T>(in PromiseCacheKey key, in Promise<T> promise)
    {
        if (promise.Task is not { IsCompletedSuccessfully: true, Result: not null })
        {
            return;
        }

        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        var clonedPromise = promise.Clone();

        foreach (var subscription in subscriptions)
        {
            if (subscription is Subscription<T> casted)
            {
                casted.OnNext(key, clonedPromise);
            }
        }
    }

    private void NotifySubscribers<T>(ReadOnlySpan<KeyAndPromise<T>> promises)
    {
        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            return;
        }

        foreach (var keyAndPromise in promises)
        {
            var key = keyAndPromise.Key;
            var promise = keyAndPromise.Promise;

            if (promise.Task is not { IsCompletedSuccessfully: true, Result: not null })
            {
                continue;
            }

            var clonedPromise = promise.Clone();

            foreach (var subscription in subscriptions)
            {
                if (subscription is Subscription<T> casted)
                {
                    casted.OnNext(key, clonedPromise);
                }
            }
        }
    }

    private sealed class Subscription<T>(
        PromiseCache2 owner,
        Action<IPromiseCache, Promise<T>> next,
        string? skipCacheKeyType) : Subscription
    {
        public void OnNext(PromiseCacheKey key, Promise<T> promise)
        {
            if (Disposed)
            {
                return;
            }

            if (promise.Task.IsCompletedSuccessfully &&
                skipCacheKeyType?.Equals(key.Type, StringComparison.Ordinal) != true)
            {
                next(owner, promise);
            }
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
}

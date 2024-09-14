using System.Diagnostics.CodeAnalysis;
using GreenDonut.Helpers;

namespace GreenDonut;

internal partial class Batch<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, IPromise> _items = [];
    private readonly Lock _lock = new();
    private BatchStatus _status = BatchStatus.Open;

    public int MaxSize { get; set; }

    public int Size => _items.Count;

    public IReadOnlyList<TKey> Keys => _status == BatchStatus.Closed ? [.. _items.Keys] : [];

    public bool TryGetOrCreatePromise<TValue>(
        PromiseCacheKey cacheKey,
        [NotNullWhen(true)] out Promise<TValue?>? promise)
    {
        if (!CanAdd())
        {
            promise = null;
            return false;
        }

        lock (_lock)
        {
            if (!CanAdd())
            {
                promise = null;
                return false;
            }

            var key = GetKey(cacheKey);

            if(_items.TryGetValue(key, out var result))
            {
                promise = (Promise<TValue?>)result;
                return true;
            }
            promise = Promise<TValue?>.Create();
            _items.Add(key, promise);
            return true;
        }
    }

    public bool TryGetOrCreatePromise<TValue, TState>(
        PromiseCacheKey cacheKey,
        Func<PromiseCacheKey, TState, Promise<TValue?>> createPromise,
        TState state,
        [NotNullWhen(true)]out Promise<TValue?>? promise)
    {
        if(!CanAdd())
        {
            promise = null;
            return false;
        }

        lock (_lock)
        {
            if (!CanAdd())
            {
                promise = null;
                return false;
            }

            var key = GetKey(cacheKey);

            promise = createPromise(cacheKey, state);
            if (_items.TryAdd(key, promise))
            {
                return true;
            }

            promise = null;
            return false;
        }
    }

    public bool TryAdd(TKey key, IPromise promise)
    {
        if (!CanAdd())
        {
            return false;
        }

        lock (_lock)
        {
            if (!CanAdd())
            {
                return false;
            }

            if (!_items.TryAdd(key, promise))
            {
                return false;
            }

            return true;
        }
    }

    public bool TryAdd(PromiseCacheKey cacheKey, IPromise promise)
    {
        return TryAdd(GetKey(cacheKey), promise);
    }

    public void EnsureScheduled<TState>(
        IBatchScheduler batchScheduler,
        Func<Batch<TKey>, TState, ValueTask> action,
        TState state)
    {
        if (_status != BatchStatus.Open)
        {
            return;
        }

        var executeBatch = false;

        lock (_lock)
        {
            if (_status != BatchStatus.Open)
            {
                return;
            }

            _status = BatchStatus.Scheduled;

            executeBatch = true;
        }

        if (executeBatch)
        {
            batchScheduler.Schedule(() => action(this, state));
        }
    }

    public Promise<TValue> GetPromise<TValue>(TKey key)
        => (Promise<TValue>)_items[key];

    public void Close()
    {
        lock (_lock)
        {
            _status = BatchStatus.Closed;
        }
    }

    private void ClearUnsafe()
    {
        MaxSize = 0;
        _items.Clear();
        _status = BatchStatus.Open;
    }

    private bool CanAdd()
    {
        if (_status == BatchStatus.Closed)
        {
            return false;
        }

        if (MaxSize > 0 && _items.Count >= MaxSize)
        {
            return false;
        }

        return true;
    }

    private static TKey GetKey(PromiseCacheKey cacheKey)
    {
        if (cacheKey.Key is not TKey key)
        {
            throw new ArgumentException($"invalid type for {cacheKey}. Expected type: {typeof(TKey)}.");
        }

        return key;
    }
}

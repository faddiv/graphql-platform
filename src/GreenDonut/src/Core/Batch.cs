using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GreenDonut.Helpers;
using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

internal partial class Batch<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, IPromise> _items = [];
    private readonly Lock _lock = new();
    private BatchStatus _status = BatchStatus.Open;
    private int _size;

    public int MaxSize { get; set; }

    public int Size => _size;

    public IReadOnlyList<TKey> Keys => _status == BatchStatus.Closed ? [.. _items.Keys] : [];

    public bool TryGetOrCreatePromise<TValue>(
        PromiseCacheKey cacheKey,
        [NotNullWhen(true)] out Promise<TValue?>? promise)
    {
        var key = GetKey(cacheKey);

        if (!CanAdd())
        {
            if(_items.TryGetValue(key, out var value))
            {
                promise = (Promise<TValue?>)value;
                return true;
            }

            promise = null;
            return false;
        }

        var holder = ValueHolder.Pool.Get();

        try
        {
            promise = (Promise<TValue?>)_items.GetOrAdd(key, static (_, state) =>
            {
                state.Promise = Promise<TValue?>.Create();
                return state.Promise;
            }, holder);

            if (ReferenceEquals(holder.Promise, promise))
            {
                Interlocked.Increment(ref _size);
            }
            return true;
        }
        finally
        {
            holder.Promise = null;
            ValueHolder.Pool.Return(holder);
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

            Interlocked.Increment(ref _size);
            return true;
        }
    }

    public void EnsureScheduled(
        IBatchScheduler batchScheduler,
        Func<ValueTask> action)
    {
        if (_status != BatchStatus.Open)
        {
            return;
        }

        bool executeBatch;

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
            batchScheduler.Schedule(action);
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

        if (MaxSize > 0 && _size >= MaxSize)
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

internal class ValueHolder
{
    public static readonly ObjectPool<ValueHolder> Pool = ObjectPool.Create<ValueHolder>();
    public IPromise? Promise { get; set; }
}

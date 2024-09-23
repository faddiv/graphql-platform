#define SAFE_BATCH

namespace GreenDonut;

#if SAFE_BATCH
using System.Diagnostics.CodeAnalysis;
using Helpers;

internal class Batch<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, IPromise> _items = [];
    private readonly Lock _lock = new();
    private BatchStatus _status = BatchStatus.Open;

    public int MaxSize { get; set; }

    public int Size
    {
        get
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }
    }

    public IReadOnlyList<TKey> Keys => _status == BatchStatus.Closed ? [.. _items.Keys] : [];

    public bool TryGetOrCreatePromise<TValue>(
        PromiseCacheKey cacheKey,
        bool allowCachePropagation,
        CancellationToken cancellationToken,
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

            if (_items.TryGetValue(key, out var result))
            {
                promise = (Promise<TValue?>)result;
                return true;
            }
            promise = Promise<TValue?>.Create(!allowCachePropagation);
            cancellationToken.Register(static state =>
            {
                ((Promise<TValue>)state!).TryCancel();
            }, promise);
            _items.Add(key, promise);
            return true;
        }
    }

    public bool NeedsScheduling()
    {
        if (_status != BatchStatus.Open)
        {
            return false;
        }

        lock (_lock)
        {
            if (_status != BatchStatus.Open)
            {
                return false;
            }

            _status = BatchStatus.Scheduled;

            return true;
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

    internal void ClearUnsafe()
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
#else

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GreenDonut.Helpers;
using Microsoft.Extensions.ObjectPool;

internal class Batch<TKey> where TKey : notnull
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
        bool allowCachePropagation,
        [NotNullWhen(true)] out Promise<TValue?>? promise)
    {
        var key = GetKey(cacheKey);

        if (!CanAdd())
        {
            if (_items.TryGetValue(key, out var value))
            {
                promise = (Promise<TValue?>)value;
                return true;
            }

            promise = null;
            return false;
        }

        var holder = ValueHolder.Pool.Get();
        holder.AllowCachePropagation = allowCachePropagation;
        try
        {
            promise = (Promise<TValue?>)_items.GetOrAdd(key, static (_, state) =>
            {
                state.Promise = Promise<TValue?>.Create(!state.AllowCachePropagation);
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

    public bool NeedsScheduling()
    {
        if (_status != BatchStatus.Open)
        {
            return false;
        }

        lock (_lock)
        {
            if (_status != BatchStatus.Open)
            {
                return false;
            }

            _status = BatchStatus.Scheduled;

            return true;
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

    internal void ClearUnsafe()
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
    public bool AllowCachePropagation { get; set; }
}
#endif

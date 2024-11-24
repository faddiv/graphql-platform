using System.Runtime.CompilerServices;

namespace GreenDonutV2;

using System.Diagnostics.CodeAnalysis;
using GreenDonut;
using Internals;

internal class Batch
{
    private readonly Dictionary<PromiseCacheKey, IPromise> _items = [];
    private readonly Lock _lock = new();
    private BatchStatusHandler _status = new();
    private int _size;

    public int MaxSize { get; set; }

    public IReadOnlyList<TKey> CollectKeys<TKey>()
    {
        var keys = new List<TKey>(_items.Count);
        foreach (var pair in _items)
        {
            keys.Add((TKey)pair.Key.Key);
        }
        return keys;
    }

    public bool TryGetOrCreatePromise<TValue>(
        PromiseCacheKey key,
        bool allowCachePropagation,
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

            if (_items.TryGetValue(key, out var result))
            {
                promise = (Promise<TValue?>)result;
                return true;
            }

            promise = Promise<TValue?>.Create(!allowCachePropagation);

            _size++;
            _items.Add(key, promise.Value);
            return true;
        }
    }

    public bool NeedsScheduling()
    {
        if (!_status.Is(BatchStatus.Open))
        {
            return false;
        }

        if (_size == 0)
        {
            return false;
        }

        var original = _status.SetStatus(BatchStatus.Scheduled, BatchStatus.Open);
        return original == BatchStatus.Open;
    }

    public Promise<TValue> GetPromise<TValue>(PromiseCacheKey key)
        => (Promise<TValue>)_items[key];

    public void Close()
    {
        lock (_lock)
        {
            _status.SetStatus(BatchStatus.Closed);
        }
    }

    internal void ClearUnsafe()
    {
        MaxSize = 0;
        _size = 0;
        _items.Clear();
        _status = new BatchStatusHandler();
    }

    private bool CanAdd()
    {
        if (_status.Is(BatchStatus.Closed))
        {
            return false;
        }

        if (MaxSize > 0 && _size >= MaxSize)
        {
            return false;
        }

        return true;
    }
}

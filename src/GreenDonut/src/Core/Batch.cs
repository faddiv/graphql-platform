using System.Collections.Concurrent;
using GreenDonut.Helpers;

namespace GreenDonut;

internal partial class Batch<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, IPromise> _items = new();
    private readonly Lock _lock = new();
    private bool _closed;

    public int Size => _items.Count;

    public IReadOnlyList<TKey> Keys => _closed ? [.. _items.Keys] : [];

    public bool TryAdd(TKey key, IPromise promise, int maxBatchSize)
    {
        if (_closed)
        {
            return false;
        }

        lock (_lock)
        {
            if (_closed)
            {
                return false;
            }

            if (maxBatchSize > 0 && _items.Count >= maxBatchSize)
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

    public void Close()
    {
        lock (_lock)
        {
            _closed = true;
        }
    }

    public Promise<TValue> GetPromise<TValue>(TKey key)
        => (Promise<TValue>)_items[key];

    private void ClearUnsafe()
    {
        _items.Clear();
        _closed = false;
    }
}

using System.Collections.Concurrent;
using GreenDonut.Helpers;

namespace GreenDonut;

internal partial class Batch<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, IPromise> _items = new();
    private readonly Lock _lock = new();
    private bool _isScheduled;

    public bool IsScheduled => _isScheduled;

    public int Size => _items.Count;

    public IReadOnlyList<TKey> Keys => _isScheduled ? [.. _items.Keys] : [];

    public bool TryAdd(TKey key, IPromise promise, int maxBatchSize)
    {
        if (_isScheduled)
        {
            return false;
        }

        lock (_lock)
        {
            if (_isScheduled)
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

    public void EnsureScheduled<TState>(
        IBatchScheduler batchScheduler,
        Func<Batch<TKey>, TState, ValueTask> action,
        TState state)
    {
        if (_isScheduled)
        {
            return;
        }

        lock (_lock)
        {
            if (_isScheduled)
            {
                return;
            }

            batchScheduler.Schedule(() => action(this, state));

            _isScheduled = true;
        }
    }

    public Promise<TValue> GetPromise<TValue>(TKey key)
        => (Promise<TValue>)_items[key];

    private void ClearUnsafe()
    {
        _items.Clear();
        _isScheduled = false;
    }
}

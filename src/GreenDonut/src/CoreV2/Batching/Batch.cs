using System.Collections.Concurrent;

namespace GreenDonutV2;

using GreenDonut;

internal class Batch<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, IPromise> _items = [];
    private BatchStatusHandler _status = new();
    private int _adding;
    private int _size;

    public int MaxSize { get; set; }

    public IReadOnlyList<TKey> Keys => _status.Is(BatchStatus.Closed) ? [.. _items.Keys] : [];

    public int Size => _size;

    public bool TryGetOrCreatePromise<TValue>(
        TKey key,
        bool allowCachePropagation,
        out Promise<TValue?> promise)
    {
        if (_items.TryGetValue(key, out var result))
        {
            promise = (Promise<TValue?>)result;
            return true;
        }

        if (!CanAdd())
        {
            promise = default;
            return false;
        }

        Interlocked.Increment(ref _adding);
        try
        {
            if (!TryReserveSlot())
            {
                promise = default;
                return false;
            }

            var newPromise = Promise<TValue?>.Create(!allowCachePropagation);
            promise = (Promise<TValue?>)_items.GetOrAdd(key, newPromise);

            if (!ReferenceEquals(promise.Task, newPromise.Task))
            {
                Interlocked.Decrement(ref _size);
            }

            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _adding);
        }
    }

    public bool TryAddPromise(TKey key, IPromise promise)
    {
        if (!CanAdd())
        {
            return false;
        }

        Interlocked.Increment(ref _adding);
        try
        {
            if (!TryReserveSlot())
            {
                return false;
            }

            // ReSharper disable once InvertIf
            if (!_items.TryAdd(key, promise))
            {
                // The execution doesn't get here.
                Interlocked.Decrement(ref _size);
                return false;
            }

            return true;

        }
        finally
        {
            Interlocked.Decrement(ref _adding);
        }
    }

    public bool NeedsScheduling()
    {
        if (!_status.Is(BatchStatus.Open))
        {
            return false;
        }

        if (Size == 0)
        {
            return false;
        }


        var original = _status.SetStatus(BatchStatus.Scheduled, BatchStatus.Open);
        return original == BatchStatus.Open;
    }

    public Promise<TValue> GetPromise<TValue>(TKey key)
        => (Promise<TValue>)_items[key];

    public void Close(CancellationToken cancellationToken)
    {
        _status.SetStatus(BatchStatus.Closed);
        var waiter = new SpinWait();
        while (Interlocked.CompareExchange(ref _adding, 0, 0) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            waiter.SpinOnce();
        }
    }

    internal void ClearUnsafe()
    {
        MaxSize = 0;
        _size = 0;
        _adding = 0;
        _items.Clear();
        _status = new BatchStatusHandler();
    }

    private bool CanAdd()
    {
        if (_status.Is(BatchStatus.Closed))
        {
            return false;
        }

        if (MaxSize > 0 && Size >= MaxSize)
        {
            return false;
        }

        return true;
    }

    private bool TryReserveSlot()
    {
        if (!CanAdd())
        {
            return false;
        }

        var size = Interlocked.Increment(ref _size);

        if (MaxSize > 0 && size > Size)
        {
            Interlocked.Decrement(ref _size);
            return false;
        }

        return true;
    }
}

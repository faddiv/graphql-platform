using System.Runtime.CompilerServices;

namespace GreenDonutV2;

using System.Diagnostics.CodeAnalysis;
using GreenDonut;
using Internals;

internal class Batch<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, IPromise> _items = [];
    private readonly Lock _lock = new();
    private BatchStatus _status = BatchStatus.Open;
    private int _size;

    public int MaxSize { get; set; }

    public IReadOnlyList<TKey> Keys => _status == BatchStatus.Closed ? [.. _items.Keys] : [];

    public bool TryGetOrCreatePromise<TValue>(
        TKey key,
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
        if (_status != BatchStatus.Open)
        {
            return false;
        }

        if (_size == 0)
        {
            return false;
        }

        var original = (BatchStatus)Interlocked.CompareExchange(
            ref Unsafe.As<BatchStatus, int>(ref _status),
            (int)BatchStatus.Scheduled,
            (int)BatchStatus.Open);
        return original == BatchStatus.Open;
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
        _size = 0;
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
}
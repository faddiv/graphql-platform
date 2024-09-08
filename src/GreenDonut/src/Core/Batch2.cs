namespace GreenDonut;

internal class Batch2<TKey> where TKey : notnull
{
    private readonly List<TKey> _keys = [];
    private readonly Dictionary<TKey, IPromise> _items = new();

    public int Size => _keys.Count;

    public IReadOnlyList<TKey> Keys => _keys;

    public bool TryAdd(TKey key, IPromise promise, int maxBatchSize)
    {
        // TODO read also needs synchronized.
        lock (this)
        {
            if (maxBatchSize > 0 && _items.Count >= maxBatchSize)
            {
                return false;
            }

            if (!_items.TryAdd(key, promise))
            {
                return false;
            }

            _keys.Add(key);
            return true;
        }
    }

    public Promise2<TValue> GetPromise<TValue>(TKey key)
        => (Promise2<TValue>)_items[key];

    internal void ClearUnsafe()
    {
        _keys.Clear();
        _items.Clear();
    }
}

using GreenDonut;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
{
    void IDataLoader<TKey, TValue>.Set(TKey key, Task<TValue?> value)
    {
        SetCacheEntry(key, value);
    }

    void IDataLoader<TKey, TValue>.Remove(TKey key)
    {
        RemoveCacheEntry(key);
    }

    /// <inheritdoc />
    Task<object?> IDataLoader.LoadAsync(
        object key,
        CancellationToken cancellationToken)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        return Load();

        async Task<object?> Load()
            => await LoadAsync((TKey)key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<object?>> IDataLoader.LoadAsync(
        IReadOnlyCollection<object> keys,
        CancellationToken cancellationToken)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        return Load();

        async Task<IReadOnlyList<object?>> Load()
        {
            var casted = keys.Select(key => (TKey)key).ToArray();
            return (IReadOnlyList<object?>)
                await LoadAsync(casted, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    void IDataLoader.RemoveCacheEntry(object key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        RemoveCacheEntry((TKey)key);
    }

    /// <inheritdoc />
    void IDataLoader.SetCacheEntry(object key, Task<object?> value)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        SetCacheEntry((TKey)key, AwaitValue());

        async Task<TValue?> AwaitValue() => (TValue)(await value.ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public void ClearCache() => Cache?.Clear();

    void IDataLoader.Set(object key, Task<object?> value)
    {
        SetCacheEntry((TKey)key, AwaitValue());

        async Task<TValue?> AwaitValue() => (TValue)(await value.ConfigureAwait(false))!;
    }

    void IDataLoader.Remove(object key)
    {
        RemoveCacheEntry((TKey)key);
    }

    void IDataLoader.Clear()
    {
        ClearCache();
    }
}

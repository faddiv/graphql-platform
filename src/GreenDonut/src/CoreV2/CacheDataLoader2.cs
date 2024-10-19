using GreenDonut;

namespace GreenDonutV2;

public abstract class CacheDataLoader2<TKey, TValue>
    : DataLoaderBase2<TKey, TValue>
    where TKey : notnull
{
    protected CacheDataLoader2(DataLoaderOptions2 options)
        : base(AutoBatchScheduler.Default, options.WithMaxBatchSize(1))
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Cache is null)
        {
            throw new ArgumentException(
                "A cache must be provided when using the CacheDataLoader.",
                nameof(options));
        }
    }

    protected internal sealed override async ValueTask FetchAsync(
        IReadOnlyList<TKey> keys,
        Memory<Result<TValue?>> results,
        DataLoaderFetchContext<TValue> context,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            try
            {
                var value = await LoadSingleAsync(keys[i], cancellationToken).ConfigureAwait(false);
                results.Span[i] = value;

            }
            catch (Exception ex)
            {
                results.Span[i] = ex;
            }
        }
    }

    protected abstract Task<TValue> LoadSingleAsync(
        TKey key,
        CancellationToken cancellationToken);
}

public abstract class StatefulCacheDataLoader2<TKey, TValue>
    : DataLoaderBase2<TKey, TValue>
    where TKey : notnull
{
    protected StatefulCacheDataLoader2(DataLoaderOptions2 options)
        : base(AutoBatchScheduler.Default, options.WithMaxBatchSize(1))
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Cache is null)
        {
            throw new ArgumentException(
                "A cache must be provided when using the CacheDataLoader.",
                nameof(options));
        }
    }

    protected internal sealed override async ValueTask FetchAsync(
        IReadOnlyList<TKey> keys,
        Memory<Result<TValue?>> results,
        DataLoaderFetchContext<TValue> context,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            try
            {
                var value = await LoadSingleAsync(keys[i], context, cancellationToken).ConfigureAwait(false);
                results.Span[i] = value;

            }
            catch (Exception ex)
            {
                results.Span[i] = ex;
            }
        }
    }

    protected abstract Task<TValue> LoadSingleAsync(
        TKey key,
        DataLoaderFetchContext<TValue> context,
        CancellationToken cancellationToken);
}

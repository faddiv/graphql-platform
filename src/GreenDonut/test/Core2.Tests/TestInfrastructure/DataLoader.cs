using GreenDonut;

namespace GreenDonutV2.TestInfrastructure;

public class DataLoader<TKey, TValue>(
    FetchDataDelegate<TKey, TValue> fetch,
    IBatchScheduler batchScheduler,
    DataLoaderOptions2? options = null)
    : DataLoaderBase2<TKey, TValue>(batchScheduler, options)
    where TKey : notnull
{
    private readonly FetchDataDelegate<TKey, TValue> _fetch =
        fetch ?? throw new ArgumentNullException(nameof(fetch));

    protected internal override ValueTask FetchAsync(
        IReadOnlyList<TKey> keys,
        Memory<Result<TValue?>> results,
        DataLoaderFetchContext<TValue> context,
        CancellationToken cancellationToken)
        => _fetch(keys, results, cancellationToken);
}
using GreenDonut;
using GreenDonut.Projections;

namespace GreenDonutV2.Projections;

internal sealed class SelectionDataLoader2<TKey, TValue>
    : DataLoaderBase2<TKey, TValue>
    , ISelectionDataLoader<TKey, TValue>
    where TKey : notnull
{
    private readonly DataLoaderBase2<TKey, TValue> _root;

    public SelectionDataLoader2(
        DataLoaderBase2<TKey, TValue> root,
        string selectionKey)
        : base(root.BatchScheduler, root.Options)
    {
        _root = root;
        CacheKeyType = $"{root.CacheKeyType}:{selectionKey}";
    }

    public IDataLoader<TKey, TValue> Root => _root;

    protected internal override string CacheKeyType { get; }

    protected override bool AllowCachePropagation => false;

    protected override bool AllowBranching => false;

    protected internal override ValueTask FetchAsync(
        IReadOnlyList<TKey> keys,
        Memory<Result<TValue?>> results,
        DataLoaderFetchContext<TValue> context,
        CancellationToken cancellationToken)
        => _root.FetchAsync(keys, results, context, cancellationToken);
}

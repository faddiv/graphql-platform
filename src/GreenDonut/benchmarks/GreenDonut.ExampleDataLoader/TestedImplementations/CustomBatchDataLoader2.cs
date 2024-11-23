using GreenDonutV2;

namespace GreenDonut.ExampleDataLoader.TestedImplementations;

// ReSharper disable once ClassNeverInstantiated.Global
public class CustomBatchDataLoader2(
    IBatchScheduler batchScheduler,
    DataLoaderOptions2 options)
    : BatchDataLoader2<string, string>(batchScheduler, options)
{
    protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
        => Task<IReadOnlyDictionary<string, string>>.Factory.StartNew(LoadBatchCore, keys, cancellationToken);

    private static IReadOnlyDictionary<string, string> LoadBatchCore(object? state)
    {
        return state is IReadOnlyList<string> keysI
            ? keysI.ToDictionary(t => t, t => "Value:" + t)
            : new Dictionary<string, string>();
    }
}

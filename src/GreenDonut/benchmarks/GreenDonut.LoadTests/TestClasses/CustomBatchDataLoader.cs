using GreenDonutV2;

namespace GreenDonut.LoadTests.TestClasses;

public class CustomBatchDataLoader(
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader<string, string>(batchScheduler, options)
{
    protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
        => Task<IReadOnlyDictionary<string, string>>.Factory.StartNew(static (state) =>
            state is IReadOnlyList<string> keysI ? keysI.ToDictionary(t => t, t => "Value:" + t) : new Dictionary<string, string>(), keys, cancellationToken);
}

public class CustomBatchDataLoader2(
    IBatchScheduler batchScheduler,
    DataLoaderOptions2 options)
    : BatchDataLoader2<string, string>(batchScheduler, options)
{
    protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
        => Task<IReadOnlyDictionary<string, string>>.Factory.StartNew(static (state) =>
            state is IReadOnlyList<string> keysI ? keysI.ToDictionary(t => t, t => "Value:" + t) : new Dictionary<string, string>(), keys, cancellationToken);
}

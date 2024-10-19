using GreenDonutV2;

namespace GreenDonut.Benchmarks.TestInfrastructure;

public class CustomBatchDataLoader2(
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader2<string, string>(batchScheduler, options)
{
    protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
        => Task<IReadOnlyDictionary<string, string>>.Factory.StartNew(static (state) =>
            state is IReadOnlyList<string> keysI ? keysI.ToDictionary(t => t, t => "Value:" + t) : new Dictionary<string, string>(), keys, cancellationToken);
}

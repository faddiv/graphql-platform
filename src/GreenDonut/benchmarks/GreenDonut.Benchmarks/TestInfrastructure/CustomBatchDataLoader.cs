namespace GreenDonut.Benchmarks.TestInfrastructure;

public class CustomBatchDataLoader : BatchDataLoader<string, string>
{
    public CustomBatchDataLoader(
        IBatchScheduler batchScheduler,
        DataLoaderOptions options)
        : base(batchScheduler, options)
    {
    }

    protected override Task<IReadOnlyDictionary<string, string>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(
            keys.ToDictionary(t => t, t => "Value:" + t));
}

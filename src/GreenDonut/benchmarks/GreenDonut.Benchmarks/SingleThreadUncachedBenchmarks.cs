using BenchmarkDotNet.Attributes;
using GreenDonut.Benchmarks.TestInfrastructure;

namespace GreenDonut.Benchmarks;

[MemoryDiagnoser]
public class SingleThreadUncachedBenchmarks
{
    private IBatchScheduler _scheduler = null!;
    private CustomBatchDataLoader _dataLoader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = AutoBatchScheduler.Default;
        _dataLoader = new CustomBatchDataLoader(
            _scheduler,
            new DataLoaderOptions());
    }

    [Benchmark]
    public Task<string?> Load()
    {
        return _dataLoader.LoadAsync("abc1");
    }
}

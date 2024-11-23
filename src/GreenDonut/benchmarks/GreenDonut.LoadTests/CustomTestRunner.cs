using GreenDonut.ExampleDataLoader;
using GreenDonut.ExampleDataLoader.TestClasses;
using GreenDonut.LoadTests.LoadTesting;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.LoadTests;

public class CustomTestRunner(TestRunnerHost root, ServiceProvider serviceProvider) : TestRunnerBase(root)
{
    private readonly ServiceProvider _serviceProvider = serviceProvider;

    protected override Task<Result> Process(CancellationToken cancel)
    {
        return Tests.ExecuteTestWith(_serviceProvider, Defaults.Version, cancel);
    }
}

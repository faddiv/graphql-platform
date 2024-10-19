using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;

namespace GreenDonut.LoadTests;

public class CustomTestRunner(TestRunnerHost root, ServiceProvider serviceProvider) : TestRunnerBase(root)
{
    private readonly ServiceProvider _serviceProvider = serviceProvider;

    protected override Task Process(CancellationToken cancel)
    {
        return Tests.ExecuteTestWith(_serviceProvider, "vNext", cancel);
    }
}

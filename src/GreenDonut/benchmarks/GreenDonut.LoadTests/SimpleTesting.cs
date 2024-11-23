using GreenDonut.ExampleDataLoader.TestClasses;
using GreenDonut.LoadTests.LoadTesting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GreenDonut.LoadTests;

public static class SimpleTesting
{
    public static async Task ExecuteNTimes(
        ServiceProvider serviceProvider,
        int count,
        string version,
        CancellationToken ct)
    {
        Console.WriteLine("Starting test runs");
        for (var i = 0; i < count; i++)
        {
            var result = await Tests.ExecuteTestWith(serviceProvider, version, ct);
            if (result.StatusCode != 200)
            {
                throw new ApplicationException($"Failed Run Detected: {result.Message}");
            }
        }
        Console.WriteLine("All runs successful");
    }


    public static async Task RunWithCustomRunner(ServiceProvider serviceProvider)
    {
        var root = new TestRunnerHost();
        for (var i = 0; i < 1; i++)
        {
            root.StartTestRunner(new CustomTestRunner(root, serviceProvider));
        }

        await Task.Delay(1000 * 60);

        await root.Stop();
    }

}

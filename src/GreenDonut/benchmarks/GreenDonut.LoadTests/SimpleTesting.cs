using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GreenDonut.LoadTests;

public class SimpleTesting
{
    public static async Task ExecuteNTimes(
        ServiceProvider serviceProvider,
        int count,
        string version,
        CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            var result = await Tests.ExecuteTestWith(serviceProvider, version, ct);
            if (result.StatusCode != 200)
            {
                AnsiConsole.MarkupLine($"[red]Failed[/] {result.StatusCode} {result.Message}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Success[/]");
            }
        }
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

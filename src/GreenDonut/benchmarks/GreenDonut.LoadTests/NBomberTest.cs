using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Contracts;
using NBomber.CSharp;

namespace GreenDonut.LoadTests;

public static class NBomberTest
{
    public static void RunWithNBomber(ServiceProvider serviceProvider, string version)
    {
        var loadSimulations = Simulation.KeepConstant(
            copies: 10,
            during: TimeSpan.FromMinutes(10));
        var warmupDuration = TimeSpan.FromSeconds(10);

        var scenario1 = Scenario.Create($"load_batch {version}",
                async context => ToResponse(await Tests.ExecuteTestWith(serviceProvider, version, context.ScenarioCancellationToken)))
            .WithLoadSimulations(
                loadSimulations
            ).WithWarmUpDuration(warmupDuration);

        NBomberRunner
            .RegisterScenarios(scenario1)
            .Run();
    }

    private static Response<object> ToResponse(Result result)
    {
        return result switch
        {
            { StatusCode: 200 } => Response.Ok(),
            _ => Response.Fail(result.StatusCode.ToString(), result.Message, 0)
        };
    }
}

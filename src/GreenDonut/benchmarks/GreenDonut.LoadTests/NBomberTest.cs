using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Contracts;
using NBomber.CSharp;

namespace GreenDonut.LoadTests;

public static class NBomberTest
{
    public static void RunWithNBomber(ServiceProvider serviceProvider)
    {
        var loadSimulations = Simulation.KeepConstant(
            copies: 10,
            during: TimeSpan.FromMinutes(3));
        var warmupDuration = TimeSpan.FromSeconds(10);

        var scenario1 = Scenario.Create("load_batch original",
                async context => ToResponse(await Tests.ExecuteTestWith(serviceProvider, "Original", context.ScenarioCancellationToken)))
            .WithLoadSimulations(
                loadSimulations
            ).WithWarmUpDuration(warmupDuration);

        NBomberRunner
            .RegisterScenarios(scenario1)
            .Run();

        var scenario2 = Scenario.Create("load_batch vNext",
                async context => ToResponse(await Tests. ExecuteTestWith(serviceProvider, "vNext", context.ScenarioCancellationToken)))
            .WithLoadSimulations(
                loadSimulations
            ).WithWarmUpDuration(warmupDuration);

        NBomberRunner
            .RegisterScenarios(scenario2)
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

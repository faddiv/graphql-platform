using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using GreenDonut;
using GreenDonut.Benchmarks;
using GreenDonut.LoadTests;
using GreenDonut.LoadTests.LoadTesting;
using GreenDonut.LoadTests.TestClasses;
using Microsoft.Extensions.DependencyInjection;
using NBomber.CSharp;
using Spectre.Console;

Console.WriteLine("Hello, World!");

var services = new ServiceCollection();
services.AddScoped<IBatchScheduler, ManualBatchScheduler>();
services.TryAddDataLoader2Core();
services.AddDataLoader<CustomBatchDataLoader>();
services.AddDataLoader<CustomBatchDataLoader2>();

var sp = services.BuildServiceProvider();

await Tests.ExecuteNTimes(sp, 100, Defaults.Original, default);
//await Tests.RunWithCustomRunner(sp);
//NBomberTest.RunWithNBomber(sp);



/*
var testRoot = new TestRunnerHost();
for (int i = 0; i < 10; i++)
{
    var tester = new TestRunnerBase(testRoot);
    testRoot.StartTestRunner(tester);
}

Console.ReadKey(true);

await testRoot.Stop();
*/

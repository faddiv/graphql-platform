using GreenDonut.Benchmarks;
using GreenDonut.LoadTests;
using GreenDonut.LoadTests.TestClasses;

Console.WriteLine("Hello, World!");

var sp = Tests.CreateServiceProvider();

await SimpleTesting.ExecuteNTimes(sp, 100, Defaults.VNext, default);
await SimpleTesting.RunWithCustomRunner(sp);
NBomberTest.RunWithNBomber(sp, Defaults.Original);
NBomberTest.RunWithNBomber(sp, Defaults.VNext);

using GreenDonut.ExampleDataLoader;
using GreenDonut.ExampleDataLoader.TestClasses;
using GreenDonut.LoadTests;

var sp = Tests.CreateServiceProvider();

await SimpleTesting.ExecuteNTimes(sp, 100, Defaults.VNext, default);
await SimpleTesting.RunWithCustomRunner(sp);
NBomberTest.RunWithNBomber(sp, Defaults.Original);
NBomberTest.RunWithNBomber(sp, Defaults.VNext);

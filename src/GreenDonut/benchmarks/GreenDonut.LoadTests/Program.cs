using GreenDonut.LoadTests.LoadTesting;
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var testRoot = new TestRunnerHost();
for (int i = 0; i < 10; i++)
{
    var tester = new TestRunnerBase(testRoot);
    testRoot.StartTestRunner(tester);
}

Console.ReadKey(true);

await testRoot.Stop();

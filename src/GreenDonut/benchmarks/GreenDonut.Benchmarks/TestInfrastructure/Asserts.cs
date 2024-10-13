using System.Runtime.CompilerServices;

namespace GreenDonut.Benchmarks.TestInfrastructure;

internal static class Asserts
{
    public static void Assert(bool condition, object? actual, [CallerArgumentExpression(nameof(condition))] string expr = "")
    {
        if (!condition)
        {
            throw new ApplicationException($"Failed: {expr} Actual: {actual}");
        }
        Console.WriteLine($"Success: {expr}");
    }

}

namespace GreenDonut.ExampleDataLoader.TestClasses.TestHelpers;

public static class Helpers
{
    public static async Task WaitAll(Task?[] tasks, int milliseconds, CancellationToken ct)
    {
        foreach (var task in tasks)
        {
            if (task is null)
            {
                return;
            }

            if (task.IsCompleted)
            {
                continue;
            }

            await task.WaitAsync(TimeSpan.FromMilliseconds(milliseconds), ct).ConfigureAwait(false);
        }
    }
}

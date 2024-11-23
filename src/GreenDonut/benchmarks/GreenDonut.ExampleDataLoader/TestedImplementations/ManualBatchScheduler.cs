using System.Collections.Concurrent;

namespace GreenDonut.ExampleDataLoader.TestedImplementations;

public class ManualBatchScheduler : IBatchScheduler
{
    private readonly ConcurrentQueue<Func<ValueTask>> _queue = new();

    public void Dispatch()
    {
        while (_queue.TryDequeue(out var dispatch))
        {
            Task.Run(async () => await dispatch());
        }
    }

    public Task DispatchAsync(CancellationToken cancel = default)
    {
        List<Task>? tasks = null;
        while (_queue.TryDequeue(out var dispatch))
        {
            tasks ??= [];
            tasks.Add(Task.Run(dispatch, cancel));
        }
        if (tasks is not null)
        {
            Task.WaitAll([.. tasks], 300, cancel);
        }
        return Task.CompletedTask;
    }

    public void Schedule(Func<ValueTask> dispatch)
    {
        _queue.Enqueue(dispatch);
    }
}

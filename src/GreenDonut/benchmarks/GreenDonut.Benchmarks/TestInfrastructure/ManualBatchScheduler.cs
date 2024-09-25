using System.Collections.Concurrent;

namespace GreenDonut.Benchmarks.TestInfrastructure;

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

    public Task DispatchAsync()
    {
        List<Task>? tasks = null;
        while (_queue.TryDequeue(out var dispatch))
        {
            tasks ??= [];
            tasks.Add(Task.Run(dispatch));
        }
        return tasks is not null
            ? Task.WhenAll(tasks)
            : Task.CompletedTask;
    }

    public void Schedule(Func<ValueTask> dispatch)
    {
        _queue.Enqueue(dispatch);
    }
}

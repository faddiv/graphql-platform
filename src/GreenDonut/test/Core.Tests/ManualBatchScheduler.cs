using System.Collections.Concurrent;

namespace GreenDonut;

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
        var tasks = new List<Task>();
        while (_queue.TryDequeue(out var dispatch))
        {

            tasks.Add(Task.Run(async () => await dispatch()));
        }
        return tasks.Count > 0
            ? Task.WhenAll(tasks)
            : Task.CompletedTask;
    }

    public void Schedule(Func<ValueTask> dispatch)
    {
        _queue.Enqueue(dispatch);
    }
}

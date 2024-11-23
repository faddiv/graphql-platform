using Microsoft.Extensions.ObjectPool;

namespace GreenDonut.ExampleDataLoader.TestClasses.TestHelpers;

public class RunCounter(int countAll) : IResettable
{
    private int _startedCount;
    private int _finishedCount;
    private readonly bool[] _runs = new bool[countAll];

    public int CountAll { get; } = countAll;

    public int StartedCount => _startedCount;

    public bool AllSucceeded
    {
        get
        {
            return _finishedCount == CountAll && _runs.All(static e => e);
        }
    }

    public int FailCount => _runs.Count(static e => !e);
    public int FinishedCount => _finishedCount;

    public void Increment()
    {
        Interlocked.Increment(ref _startedCount);
    }

    public void Finished(int index, bool status)
    {
        Interlocked.Increment(ref _finishedCount);
        _runs[index] = status;
    }

    public bool TryReset()
    {
        var finished = _finishedCount == CountAll;
        _startedCount = 0;
        _finishedCount = 0;
        Array.Clear(_runs, 0, _runs.Length);
        return finished;
    }
}

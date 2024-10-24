// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics;

namespace GreenDonut.LoadTests.LoadTesting;

public class TestRunnerHost
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private int _id;
    private readonly List<Task> _runners = new List<Task>();
    private Task? _collector = null;

    public TestRunnerHost()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }
    public BlockingCollection<Results> Results { get; } = new BlockingCollection<Results>();

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public void StartTestRunner(TestRunnerBase tester)
    {
        if (_collector is null)
        {
            _collector = Task.Run(CollectorProcess);
            _runners.Add(_collector);
        }
        _runners.Add(tester.Run());
    }

    private void CollectorProcess()
    {
        try
        {
            var collect = new List<Results>(1024);
            var durationSum = 0L;
            var durationCount = 0L;
            var pauseSum = 0L;
            var pauseCount = 0L;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var time = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(time) < TimeSpan.FromSeconds(1))
                {

                    var result = Results.Take(CancellationToken);
                    collect.Add(result);
                }

                var memory = GC.GetGCMemoryInfo();
                var duration = collect.LongAverage(e => e.Duration);
                durationSum += duration;
                durationCount++;
                var pause = memory.PauseDurations.DurationAverage(e => e);
                var durationAvg = TimeSpan.FromTicks(durationSum/durationCount);
                pauseSum += pause.Ticks;
                pauseCount++;
                var pauseAvg = TimeSpan.FromTicks(pauseSum/pauseCount);
                Console.WriteLine($"Duration: {TimeSpan.FromTicks(duration)} Pause: {pause}" +
                    $" DurationAvg:{durationAvg} PauseAvg: {pauseAvg}");

                collect.Clear();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal async Task Stop()
    {
        await _cancellationTokenSource.CancelAsync();
        await Task.WhenAll(_runners);
    }

    internal int CreateId()
    {
        return ++_id;
    }
}

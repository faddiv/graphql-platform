// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics;

namespace GreenDonut.LoadTests.LoadTesting;

public class TestRunnerHost
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private int _id;
    private readonly List<Task> _runners = [];
    private Task? _collector;

    public BlockingCollection<Results> Results { get; } = new();

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public void StartTestRunner(TestRunnerBase tester)
    {
        if (_collector is null)
        {
            _collector = Task.Run(CollectorProcess, CancellationToken);
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
            var successSum = 0L;
            var sumCount = 0L;
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
                successSum += collect.Count(e => e.Success);
                sumCount += collect.Count;
                durationSum += duration;
                durationCount++;
                var pause = memory.PauseDurations.DurationAverage(e => e);
                var durationAvg = TimeSpan.FromTicks(durationSum/durationCount);
                pauseSum += pause.Ticks;
                pauseCount++;
                var pauseAvg = TimeSpan.FromTicks(pauseSum/pauseCount);
                Console.WriteLine($"DurationAvg:{durationAvg} PauseAvg: {pauseAvg} SuccessRate: {(double)successSum/sumCount:P}");

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

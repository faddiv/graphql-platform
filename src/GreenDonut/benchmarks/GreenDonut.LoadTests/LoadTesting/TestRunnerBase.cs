using System.Diagnostics;
using System.Runtime.CompilerServices;
using NBomber;

namespace GreenDonut.LoadTests.LoadTesting;

public class TestRunnerBase(TestRunnerHost root)
{
    private readonly TestRunnerHost _root = root;
    public int Id = root.CreateId();

    public Task Run()
    {
        return Task.Run(async () =>
        {
            var cancel = _root.CancellationToken;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancel, cts.Token);
            while (!cancel.IsCancellationRequested)
            {
                cts.CancelAfter(TimeSpan.FromSeconds(1));
                var time = Stopwatch.GetTimestamp();
                try
                {
                    await Process(linkedCts.Token);
                    var duration = Stopwatch.GetElapsedTime(time);

                    _root.Results.Add(new Results
                    {
                        Id = Id,
                        Duration = duration.Ticks,
                        Success = true
                    }, cancel);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    var duration = Stopwatch.GetElapsedTime(time);
                    _root.Results.Add(new Results
                    {
                        Id = Id,
                        Duration = duration.Ticks,
                        Success = false,
                        Exception = ex
                    }, cancel);
                }
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual async Task Process(CancellationToken cancel)
    {
        await Task.Delay(100, cancel);
    }
}

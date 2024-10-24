using System.Diagnostics;
using System.Runtime.CompilerServices;
using GreenDonut.LoadTests.TestClasses;

namespace GreenDonut.LoadTests.LoadTesting;

public abstract class TestRunnerBase(TestRunnerHost root)
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
                    var result = await Process(linkedCts.Token);
                    var duration = Stopwatch.GetElapsedTime(time);

                    _root.Results.Add(new Results
                    {
                        Id = Id,
                        Duration = duration.Ticks,
                        Success = result.StatusCode == 200
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
    protected abstract Task<Result> Process(CancellationToken cancel);
}

namespace GreenDonut.LoadTests;

public static class ResultCollectorExtensions
{
    public static long LongAverage<TResult>(this ICollection<TResult> results, Func<TResult, long> selector)
    {
        var count = results.Count;
        return results.Aggregate(0L, (sum, e) => sum + selector(e), sum => sum / count);
    }
    public static TimeSpan DurationAverage<TResult>(this ReadOnlySpan<TResult> results, Func<TResult, TimeSpan> selector)
    {
        var count = results.Length;
        var sum = 0L;
        foreach (var item in results)
        {
            sum += selector(item).Ticks;
        }
        return TimeSpan.FromTicks(sum / count);
    }
}

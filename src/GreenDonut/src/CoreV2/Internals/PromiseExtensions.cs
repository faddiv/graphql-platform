using GreenDonut;

namespace GreenDonutV2.Internals;

internal static class PromiseExtensions
{
    public static Promise<TValue> As<TValue>(this IPromise? basePromise)
    {
        return basePromise is Promise<TValue> promise
            ? promise
            : throw new InvalidCastException($"Can not cast {basePromise?.GetType().FullName ?? "null"} to Promise<{typeof(TValue).FullName}>");
    }
}

internal static class InternalHelpers
{
    private const int MinimumSize = 10;

    public static int CalculateSize(int size)
    {
        return Math.Max(size, MinimumSize);
    }
    public static int CalculateLockThreshold(int size)
    {
        size = CalculateSize(size);
        return size - (int)Math.Max(size * 0.1, 10);
    }
}

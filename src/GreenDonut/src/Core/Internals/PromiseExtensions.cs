namespace GreenDonut.Internals;

internal static class PromiseExtensions
{
    public static Promise<TValue> As<TValue>(this IPromise? basePromise)
    {
        return basePromise is Promise<TValue> promise
            ? promise
            : throw new InvalidCastException($"Can not cast {basePromise?.GetType().FullName ?? "null"} to Promise<{typeof(TValue).FullName}>");
    }
}

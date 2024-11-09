using GreenDonut;

namespace GreenDonutV2.Internals;

public readonly struct KeyAndPromise<TValue>(PromiseCacheKey key, Promise<TValue> promise)
{
    public PromiseCacheKey Key { get; } = key;
    public Promise<TValue> Promise { get; } = promise;
}

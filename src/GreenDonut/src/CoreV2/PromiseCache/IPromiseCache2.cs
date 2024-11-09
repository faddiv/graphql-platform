using GreenDonut;
using GreenDonutV2.Internals;

namespace GreenDonutV2;

/// <summary>
/// A memorization cache for <c>DataLoader</c>.
/// </summary>
public interface IPromiseCache2 :IPromiseCache
{
    bool TryGetPromise<TValue>(PromiseCacheKey cacheKey, out Promise<TValue?> promise);

    void TryAddMany<TValue>(ReadOnlySpan<KeyAndPromise<TValue>> promises);
}

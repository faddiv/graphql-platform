using GreenDonut;

namespace GreenDonutV2;

/// <summary>
/// A memorization cache for <c>DataLoader</c>.
/// </summary>
public interface IPromiseCache2 :IPromiseCache
{

    /// <summary>
    /// Gets a promise from the cache if a promise with the specified <paramref name="key"/> already
    /// exists; otherwise, the <paramref name="createPromise"/> factory is used to create a new
    /// task and add it to the cache.
    /// </summary>
    /// <param name="key">A cache entry key.</param>
    /// <param name="createPromise">A factory to create the new task.</param>
    /// <param name="state">A state value to pass the createPromise function.</param>
    /// <param name="promise">Either the retrieved task from the cache or new one.</param>
    /// <typeparam name="T">The task type.</typeparam>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <returns>
    /// Returns true if the promise get fron the cache.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Throws if <paramref name="key"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Throws if <paramref name="createPromise"/> is <c>null</c>.
    /// </exception>
    bool TryGetOrAddPromise<T, TState>(
        PromiseCacheKey key,
        Func<PromiseCacheKey, TState, Promise<T>> createPromise,
        TState state,
        out Promise<T> promise);
}

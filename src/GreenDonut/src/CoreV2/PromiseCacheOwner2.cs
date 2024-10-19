using GreenDonut;
using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

/// <summary>
/// Owner of <see cref="PromiseCache"/> that is responsible for returning the rented
/// <see cref="PromiseCache"/> appropriately to the <see cref="ObjectPool{TaskCache}"/>.
/// </summary>
public sealed class PromiseCacheOwner2 : IDisposable
{
    private readonly ObjectPool<PromiseCache2> _pool;
    private readonly PromiseCache2 _cache;
    private bool _disposed;

    /// <summary>
    /// Rents a new cache from <see cref="PromiseCachePool.Shared"/>.
    /// </summary>
    public PromiseCacheOwner2()
    {
        _pool = PromiseCachePool2.Shared;
        _cache = PromiseCachePool2.Shared.Get();
    }

    /// <summary>
    /// Rents a new cache from the given <paramref name="pool"/>.
    /// </summary>
    public PromiseCacheOwner2(ObjectPool<PromiseCache2> pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _cache = pool.Get();
    }

    /// <summary>
    /// Gets the rented cache.
    /// </summary>
    public IPromiseCache2 Cache => _cache;

    /// <summary>
    /// Returns the rented cache back to the <see cref="ObjectPool{TaskCache}"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Return(_cache);
            _disposed = true;
        }
    }
}

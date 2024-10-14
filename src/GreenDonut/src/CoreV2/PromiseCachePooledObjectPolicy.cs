using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

internal sealed class PromiseCachePooledObjectPolicy(int size) : PooledObjectPolicy<PromiseCache>
{
    public override PromiseCache Create() => new(size);

    public override bool Return(PromiseCache obj)
    {
        obj.Clear();
        return true;
    }
}

using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

internal sealed class PromiseCachePooledObjectPolicy2(int size) : PooledObjectPolicy<PromiseCache2>
{
    public override PromiseCache2 Create() => new(size);

    public override bool Return(PromiseCache2 obj)
    {
        obj.Clear();
        return true;
    }
}

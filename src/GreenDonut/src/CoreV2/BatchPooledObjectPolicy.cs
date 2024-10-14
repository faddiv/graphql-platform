using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

internal class BatchPooledObjectPolicy<TKey>
        : PooledObjectPolicy<Batch<TKey>>
     where TKey : notnull
{
    public override Batch<TKey> Create() => new();

    public override bool Return(Batch<TKey> obj)
    {
        obj.ClearUnsafe();
        return true;
    }
}

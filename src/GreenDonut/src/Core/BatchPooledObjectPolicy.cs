using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

internal class BatchPooledObjectPolicy<TKey>
        : PooledObjectPolicy<Batch<TKey>>
{
    public override Batch<TKey> Create() => new();

    public override bool Return(Batch<TKey> obj)
    {
        obj.ClearUnsafe();
        return true;
    }
}

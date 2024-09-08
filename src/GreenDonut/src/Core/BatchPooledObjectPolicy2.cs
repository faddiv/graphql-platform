using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

internal class BatchPooledObjectPolicy2<TKey>
    : PooledObjectPolicy<Batch2<TKey>>
    where TKey : notnull
{
    public override Batch2<TKey> Create() => new();

    public override bool Return(Batch2<TKey> obj)
    {
        obj.ClearUnsafe();
        return true;
    }
}

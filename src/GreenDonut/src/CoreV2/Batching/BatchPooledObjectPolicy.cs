using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

internal class BatchPooledObjectPolicy
        : PooledObjectPolicy<Batch>
{
    public override Batch Create() => new();

    public override bool Return(Batch obj)
    {
        obj.ClearUnsafe();
        return true;
    }
}

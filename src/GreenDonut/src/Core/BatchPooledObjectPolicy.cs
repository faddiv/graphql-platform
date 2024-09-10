using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

partial class Batch<TKey>
{
    internal class BatchPooledObjectPolicy
        : PooledObjectPolicy<Batch<TKey>>
    {
        public override Batch<TKey> Create() => new();

        public override bool Return(Batch<TKey> obj)
        {
            obj.ClearUnsafe();
            return true;
        }
    }
}

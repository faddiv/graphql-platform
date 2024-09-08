using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

partial class Batch2<TKey>
{
    internal class BatchPooledObjectPolicy2
        : PooledObjectPolicy<Batch2<TKey>>
    {
        public override Batch2<TKey> Create() => new();

        public override bool Return(Batch2<TKey> obj)
        {
            obj.ClearUnsafe();
            return true;
        }
    }
}

using Microsoft.Extensions.ObjectPool;

namespace GreenDonut;

internal static class BatchPool2<TKey> where TKey : notnull
{
    public static ObjectPool<Batch2<TKey>> Shared { get; } = Create();

    private static ObjectPool<Batch2<TKey>> Create()
        => new DefaultObjectPool<Batch2<TKey>>(
            new BatchPooledObjectPolicy2<TKey>(),
            Environment.ProcessorCount * 4);
}

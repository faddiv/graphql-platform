using Microsoft.Extensions.ObjectPool;

namespace GreenDonutV2;

internal static class BatchPool
{
    public static ObjectPool<Batch> Shared { get; } = Create();

    private static ObjectPool<Batch> Create()
        => new DefaultObjectPool<Batch>(
            new BatchPooledObjectPolicy(),
            Environment.ProcessorCount * 4);
}

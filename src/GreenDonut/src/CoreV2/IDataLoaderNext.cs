using GreenDonut;

namespace GreenDonutV2;

public interface IDataLoaderNext : IDataLoader
{
    void RemoveCacheEntry(object key);
    void SetCacheEntry(object key, Task<object?> value);
}

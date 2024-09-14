using System.Collections.Concurrent;

namespace GreenDonut.Helpers;

public static class CompatibilityExtensions
{
    public static bool IsCompletedSuccessfully(this Task task)
    {
#if NETSTANDARD2_0
        return task.Status == TaskStatus.RanToCompletion;
#else
        return task.IsCompletedSuccessfully;
#endif
    }

#if NETSTANDARD2_0
    public static TValue GetOrAdd<TKey, TValue, TArg>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument) where TKey : notnull
    {
        return dictionary.GetOrAdd(
            key,
            k => valueFactory(k, factoryArgument));
    }

    public static bool TryAdd<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key, TValue vaule) where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
        {
            return false;
        }
        dictionary.Add(key, vaule);
        return true;
    }
#endif
}

#if NET9_0_OR_GREATER
#else
internal class Lock
{

}
#endif

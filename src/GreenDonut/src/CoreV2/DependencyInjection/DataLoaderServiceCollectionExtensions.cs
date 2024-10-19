using GreenDonut;
using GreenDonutV2;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.Extensions.DependencyInjection;

public static class DataLoaderServiceCollectionExtensions
{
    public static IServiceCollection TryAddDataLoader2Core(
        this IServiceCollection services)
    {
        services.TryAddSingleton(sp => PromiseCachePool2.Create(sp.GetRequiredService<ObjectPoolProvider>()));
        services.TryAddScoped(sp => new PromiseCacheOwner2(sp.GetRequiredService<ObjectPool<PromiseCache2>>()));

        services.TryAddScoped(
            sp =>
            {
                var cacheOwner = sp.GetRequiredService<PromiseCacheOwner2>();

                return new DataLoaderOptions2
                {
                    Cache = cacheOwner.Cache,
                    DiagnosticEvents = sp.GetService<IDataLoaderDiagnosticEvents>(),
                    MaxBatchSize = 1024,
                };
            });

        return services;
    }
}

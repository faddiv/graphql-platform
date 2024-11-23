using Microsoft.Extensions.ObjectPool;

namespace GreenDonut.ExampleDataLoader.TestClasses.TestHelpers;

public class RunCounterObjectPoolPolicy : IPooledObjectPolicy<RunCounter>
{
    public RunCounter Create()
    {
        return new RunCounter(100);
    }

    public bool Return(RunCounter obj)
    {
        return obj.TryReset();
    }
}

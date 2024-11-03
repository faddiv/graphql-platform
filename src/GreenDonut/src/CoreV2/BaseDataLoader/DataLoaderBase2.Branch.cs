using GreenDonut;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
{
    /// <inheritdoc />
    public IDataLoader Branch<TState>(
        string key,
        CreateDataLoaderBranch<TKey, TValue, TState> createBranch,
        TState state)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(key));
        }

        if (createBranch == null)
        {
            throw new ArgumentNullException(nameof(createBranch));
        }

        if (!AllowBranching)
        {
            throw new InvalidOperationException(
                "Branching is not allowed for this DataLoader.");
        }

        if (_branches.TryGetValue(key, out var branch))
        {
            return branch;
        }

        lock (_branchesLock)
        {
            if (_branches.TryGetValue(key, out branch))
            {
                return branch;
            }

            var newBranch = createBranch(key, this, state);
            _branches = _branches.Add(key, newBranch);
            return newBranch;
        }
    }
}

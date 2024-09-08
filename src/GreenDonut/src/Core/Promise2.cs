using GreenDonut.Helpers;

namespace GreenDonut;

/// <summary>
/// Represents a promise to fetch data within the DataLoader.
/// A promise can be based on the actual value,
/// a <see cref="Task{TResult}"/>,
/// or a <see cref="TaskCompletionSource{TResult}"/>.
/// </summary>
/// <typeparam name="TValue"></typeparam>
public class Promise2<TValue> : IPromise
{
    private readonly TaskCompletionSource<TValue>? _completionSource;
    private volatile bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="Promise{TValue}"/> class
    /// </summary>
    /// <param name="value">
    /// The actual value of the promise.
    /// </param>
    public Promise2(TValue value)
    {
        Task = System.Threading.Tasks.Task.FromResult(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Promise{TValue}"/> class
    /// </summary>
    /// <param name="task">
    /// The task that represents the promise.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> is <c>null</c>.
    /// </exception>
    public Promise2(Task<TValue> task)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
    }

    private Promise2(TaskCompletionSource<TValue> completionSource)
    {
        _completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
        Task = completionSource.Task;
    }

    private Promise2(Task<TValue> task, TaskCompletionSource<TValue>? completionSource, bool isClone)
    {
        Task = task;
        _completionSource = completionSource;
        IsClone = isClone;
    }

    /// <summary>
    /// Gets the task that represents the promise.
    /// </summary>
    public Task<TValue> Task { get; }

    /// <inheritdoc />
    public bool IsClone { get; }

    Task IPromise.Task => Task;

    Type IPromise.Type => typeof(TValue);

    /// <summary>
    /// Tries to set the result of the async work for this promise.
    /// </summary>
    /// <param name="result">
    /// The result of the async work.
    /// </param>
    public void TrySetResult(TValue result)
        => _completionSource?.TrySetResult(result);

    void IPromise.TrySetResult(object? result)
    {
        if (result is not TValue value)
        {
            throw new ArgumentException(
                "The result is not of the expected type.",
                nameof(result));
        }

        TrySetResult(value);
    }

    /// <inheritdoc />
    public void TrySetError(Exception exception)
        => _completionSource?.TrySetException(exception);

    /// <inheritdoc />
    public void TryCancel()
        => _completionSource?.TrySetCanceled();

    /// <summary>
    /// Registers a callback that will be called when the promise is completed.
    /// </summary>
    /// <param name="callback">
    /// The callback that will be called when the promise is completed.
    /// </param>
    /// <param name="state">
    /// The state that will be passed to the callback.
    /// </param>
    /// <typeparam name="TState">
    /// The type of the state.
    /// </typeparam>
    public void OnComplete<TState>(
        Action<Promise2<TValue>, TState> callback,
        TState state)
    {
        if (IsClone)
        {
            throw new InvalidCastException(
                "The promise is a clone and cannot be used to register a callback.");
        }

        Task.ContinueWith(
            (task, s) =>
            {
                if (task.IsCompletedSuccessfully()
                    && task.Result is not null)
                {
                    callback(new Promise2<TValue>(task.Result), (TState)s!);
                }
            },
            state,
            TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    /// <summary>
    /// Clones this promise.
    /// </summary>
    /// <returns>
    /// Returns a new instance of <see cref="Promise{TValue}"/>.
    /// </returns>
    public Promise2<TValue> Clone()
        => new(Task, null, isClone: true);

    IPromise IPromise.Clone() => Clone();

    /// <summary>
    /// Creates a new promise for the specified value type.
    /// </summary>
    /// <param name="cloned">
    /// Marks the promise as a clone.
    /// </param>
    /// <returns>
    /// Returns a new instance of <see cref="Promise{TValue}"/>.
    /// </returns>
    public static Promise2<TValue> Create(bool cloned = false)
    {
        var taskCompletionSource = new TaskCompletionSource<TValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        return new Promise2<TValue>(taskCompletionSource.Task, taskCompletionSource, cloned);
    }

    /// <summary>
    /// Creates a new completed promise for the specified value.
    /// </summary>
    /// <param name="value">
    /// The value of the promise.
    /// </param>
    /// <param name="cloned">
    /// Marks the promise as a clone.
    /// </param>
    /// <returns>
    /// Returns a new instance of <see cref="Promise{TValue}"/>.
    /// </returns>
    public static Promise2<TValue> Create(TValue value, bool cloned = true)
        => new(System.Threading.Tasks.Task.FromResult(value), null, isClone: true);

    /// <summary>
    /// Implicitly converts a <see cref="TaskCompletionSource{TResult}"/> to a promise.
    /// </summary>
    /// <param name="promise">
    /// The <see cref="TaskCompletionSource{TResult}"/> to convert.
    /// </param>
    /// <returns>
    /// The promise that represents the task completion source.
    /// </returns>
    public static implicit operator Promise2<TValue>(TaskCompletionSource<TValue> promise)
        => new(promise);

    public bool TryInitialize<TKey>(
        DataLoaderBase2<TKey,TValue> dataLoaderBase2, TKey key,
        Action<DataLoaderBase2<TKey,TValue>, TKey, Promise2<TValue>> initialize) where TKey : notnull
    {
        if (_initialized)
        {
            return false;
        }
        // TODO Dont need to wait for lock release. If the lock aquired, then doest need.
        lock (Task)
        {
            if (_initialized)
            {
                return false;
            }

            initialize(dataLoaderBase2, key, this);

            _initialized = true;
        }
        return true;
    }
}

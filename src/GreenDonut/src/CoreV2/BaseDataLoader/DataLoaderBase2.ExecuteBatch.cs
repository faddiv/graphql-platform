using System.Buffers;
using GreenDonut;
using GreenDonutV2.Internals;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
{
    private Batch CreateNewBatch(Batch? oldBatch)
    {
        var newBatch = _currentBatch;
        if (newBatch is not null && newBatch != oldBatch)
        {
            return newBatch;
        }

        lock (_batchExchangeLock)
        {
            newBatch = _currentBatch;
            if (newBatch is not null && newBatch != oldBatch)
            {
                return newBatch;
            }

            newBatch = BatchPool.Shared.Get();
            newBatch.MaxSize = _maxBatchSize;
            _currentBatch = newBatch;
            return newBatch;
        }
    }

    private void EnsureBatchExecuted(Batch? batch, string cacheKeyType, CancellationToken cancellationToken)
    {
        if (batch?.NeedsScheduling() ?? false)
        {
            ExecuteBatchInternal(batch, cacheKeyType, cancellationToken);
        }
    }

    private void ExecuteBatchInternal(Batch batch, string cacheKeyType, CancellationToken cancellationToken)
    {
        _batchScheduler.Schedule(() => ExecuteBatch(batch, cacheKeyType, cancellationToken));
    }

    private async ValueTask ExecuteBatch(Batch batch, string cacheKeyType, CancellationToken cancellationToken)
    {
        //Remove the batch only if it is still the same.
        Interlocked.CompareExchange(ref _currentBatch, null, batch);

        batch.Close();

        var errors = false;

        var keys = batch.CollectKeys<TKey>();

        using (_diagnosticEvents.ExecuteBatch(this, keys))
        {
            var array = ArrayPool<Result<TValue?>>.Shared.Rent(keys.Count);
            var buffer = array.AsMemory(0, keys.Count);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new DataLoaderFetchContext<TValue>(ContextData);
                await FetchAsync(keys, buffer, context, cancellationToken).ConfigureAwait(false);
                BatchOperationSucceeded(batch, keys, cacheKeyType, buffer.Span);
            }
            catch (Exception ex)
            {
                errors = true;
                BatchOperationFailed(batch, keys, cacheKeyType, ex);
            }
            finally
            {
                ArrayPool<Result<TValue?>>.Shared.Return(array, clearArray: true);
            }
        }

        // we return the batch here so that the keys are only cleared
        // after the diagnostic events are done.
        if (!errors)
        {
            BatchPool.Shared.Return(batch);
        }
    }

    private void BatchOperationFailed(Batch batch, IReadOnlyList<TKey> keys, string cacheKeyType, Exception error)
    {
        _diagnosticEvents.BatchError(keys, error);

        foreach (var key in keys)
        {
            PromiseCacheKey cacheKey = new(cacheKeyType, key);
            if (Cache is not null)
            {
                Cache.TryRemove(cacheKey);
            }

            batch.GetPromise<TValue>(cacheKey).TrySetError(error);
        }
    }

    private void BatchOperationSucceeded(
        Batch batch,
        IReadOnlyList<TKey> keys,
        string cacheKeyType,
        ReadOnlySpan<Result<TValue?>> results)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var value = results[i];

            if (value.Kind is ResultKind.Undefined)
            {
                // in case we got here less or more results as expected, the
                // complete batch operation failed.
                Exception error = Errors.CreateKeysAndValuesMustMatch(keys.Count, i);
                BatchOperationFailed(batch, keys, cacheKeyType, error);
                return;
            }

            PromiseCacheKey cacheKey = new(cacheKeyType, key);
            SetSingleResult(batch.GetPromise<TValue?>(cacheKey), key, value);
        }

        _diagnosticEvents.BatchResults(keys, results);
    }

    private void SetSingleResult(Promise<TValue?> promise, TKey key, Result<TValue?> result)
    {
        if (result.Kind is ResultKind.Value)
        {
            promise.TrySetResult(result);
        }
        else
        {
            _diagnosticEvents.BatchItemError(key, result.Error!);
            promise.TrySetError(result.Error!);
        }
    }
}

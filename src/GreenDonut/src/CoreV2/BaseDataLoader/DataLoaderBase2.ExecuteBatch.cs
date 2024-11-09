using System.Buffers;
using System.Runtime.InteropServices;
using GreenDonut;
using GreenDonutV2.Internals;

namespace GreenDonutV2;

public abstract partial class DataLoaderBase2<TKey, TValue>
{
    private Batch<TKey> CreateNewBatch(Batch<TKey>? oldBatch)
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

            newBatch = BatchPool<TKey>.Shared.Get();
            newBatch.MaxSize = _maxBatchSize;
            _currentBatch = newBatch;
            return newBatch;
        }
    }

    private void EnsureBatchExecuted(Batch<TKey>? batch, CancellationToken cancellationToken)
    {
        if (batch?.NeedsScheduling() ?? false)
        {
            ExecuteBatchInternal(batch, cancellationToken);
        }
    }

    private void ExecuteBatchInternal(Batch<TKey> batch, CancellationToken cancellationToken)
    {
        _batchScheduler.Schedule(() => ExecuteBatch(batch, cancellationToken));
    }

    private async ValueTask ExecuteBatch(Batch<TKey> batch, CancellationToken cancellationToken)
    {
        //Remove the batch only if it is still the same.
        Interlocked.CompareExchange(ref _currentBatch, null, batch);

        batch.Close(cancellationToken);

        var errors = false;

        var keys = batch.Keys;

        using (_diagnosticEvents.ExecuteBatch(this, keys))
        {
            var array = ArrayPool<Result<TValue?>>.Shared.Rent(keys.Count);
            var buffer = array.AsMemory(0, keys.Count);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new DataLoaderFetchContext<TValue>(ContextData);
                await FetchAsync(keys, buffer, context, cancellationToken).ConfigureAwait(false);
                BatchOperationSucceeded(batch, keys, buffer.Span);
            }
            catch (Exception ex)
            {
                errors = true;
                BatchOperationFailed(batch, keys, ex);
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
            BatchPool<TKey>.Shared.Return(batch);
        }
    }

    private void BatchOperationFailed(Batch<TKey> batch, IReadOnlyList<TKey> keys, Exception error)
    {
        _diagnosticEvents.BatchError(keys, error);

        foreach (var key in keys)
        {
            if (Cache is not null)
            {
                PromiseCacheKey cacheKey = new(CacheKeyType, key);
                Cache.TryRemove(cacheKey);
            }

            batch.GetPromise<TValue>(key).TrySetError(error);
        }
    }

    private void BatchOperationSucceeded(
        Batch<TKey> batch,
        IReadOnlyList<TKey> keys,
        ReadOnlySpan<Result<TValue?>> results)
    {
        var promiseArray = ArrayPool<KeyAndPromise<TValue?>>.Shared.Rent(keys.Count);
        var promises = promiseArray.AsSpan(0, keys.Count);
        try
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
                    BatchOperationFailed(batch, keys, error);
                    return;
                }

                var promise = batch.GetPromise<TValue?>(key);
                promises[i] = new KeyAndPromise<TValue?>(new PromiseCacheKey(CacheKeyType, key), promise);
                SetSingleResult(promise, key, value);
            }

            Cache?.TryAddMany<TValue?>(promises);
        }
        finally
        {
            ArrayPool<KeyAndPromise<TValue?>>.Shared.Return(promiseArray, clearArray: true);
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

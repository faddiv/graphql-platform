namespace GreenDonutV2;

internal record struct BatchStatusHandler
{
    private int _status = (int)BatchStatus.Open;

    public BatchStatusHandler()
    {
    }

    public bool Is(BatchStatus status)
    {
        return status == GetStatus();
    }

    public BatchStatus SetStatus(BatchStatus batchStatus)
    {
        return (BatchStatus)Interlocked.Exchange(ref _status, (int)batchStatus);
    }

    public BatchStatus SetStatus(BatchStatus batchStatus, BatchStatus fromStatus)
    {
        return (BatchStatus)Interlocked.CompareExchange(ref _status, (int)batchStatus, (int)fromStatus);
    }

    public override string ToString()
    {
        return GetStatus().ToString();
    }

    private BatchStatus GetStatus()
    {
        return (BatchStatus)Interlocked.CompareExchange(ref _status, 0, 0);
    }
}

namespace VolvoJ2534.App;

internal sealed class CanBus : IDisposable
{
    private readonly J2534Native _j2534;
    private readonly CanRxDispatcher _rx;
    private readonly SemaphoreSlim _txLock = new(1, 1);
    private int _disposed;

    internal CanBus(J2534Native j2534)
    {
        _j2534 = j2534 ?? throw new ArgumentNullException(nameof(j2534));
        _rx = new CanRxDispatcher(_j2534);
    }

    internal event Action<Exception>? ReadError
    {
        add => _rx.ReadError += value;
        remove => _rx.ReadError -= value;
    }

    internal CanRxDispatcher.Subscription Subscribe()
    {
        ThrowIfDisposed();
        return _rx.Subscribe();
    }

    internal void Start()
    {
        ThrowIfDisposed();
        _rx.Start();
    }

    internal void Stop()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        _rx.Stop();
    }

    internal bool Send(
        uint arbitrationId,
        ReadOnlySpan<byte> data,
        bool extended,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        out string error)
    {
        ThrowIfDisposed();

        if (data.Length > 8)
            throw new ArgumentOutOfRangeException(nameof(data),
                "Classic CAN payload cannot exceed 8 bytes.");

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        cancellationToken.ThrowIfCancellationRequested();

        _txLock.Wait(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var message = CanDecoder.Encode(arbitrationId, data, extended);
            var timeoutMs = (uint)Math.Clamp(
                (long)Math.Ceiling(timeout.TotalMilliseconds), 1, uint.MaxValue);

            return _j2534.Write(message, timeoutMs, out error);
        }
        finally
        {
            _txLock.Release();
        }
    }

    internal bool TrySend(
        uint arbitrationId,
        ReadOnlySpan<byte> data,
        bool extended,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        out string error)
    {
        return Send(arbitrationId, data, extended, timeout, cancellationToken, out error);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(CanBus));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _rx.Dispose();
        _txLock.Dispose();
    }
}

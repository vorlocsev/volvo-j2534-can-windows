using System.Diagnostics;

namespace VolvoJ2534.App;

internal sealed class CanBus : IDisposable
{
    private readonly IJ2534Adapter _j2534;
    private readonly CanRxDispatcher _rx;
    private readonly SemaphoreSlim _txLock = new(1, 1);
    private int _disposed;

    internal CanBus(IJ2534Adapter j2534)
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

        // The timeout is a total budget for both waiting on the TX lock and
        // the native J2534 write, not a fresh budget for each stage.
        var elapsed = Stopwatch.StartNew();
        if (!_txLock.Wait(timeout, cancellationToken))
        {
            error = "Timed out waiting for the CAN transmit lock.";
            return false;
        }

        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                error = "CAN transmit timeout expired before adapter write.";
                return false;
            }

            var message = CanDecoder.Encode(arbitrationId, data, extended);
            var timeoutMs = (uint)Math.Clamp(
                (long)Math.Ceiling(remaining.TotalMilliseconds), 1, uint.MaxValue);

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
        // Do not dispose the semaphore while a Send call may still be in
        // its finally block. A concurrent Dispose would make Release throw.
        // The semaphore is managed and safe to leave for collection.
    }
}

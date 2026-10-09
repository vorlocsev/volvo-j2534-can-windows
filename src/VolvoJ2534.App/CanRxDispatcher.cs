using System.Threading.Channels;

namespace VolvoJ2534.App;

internal sealed class CanRxDispatcher : IDisposable
{
    internal sealed class Subscription : IDisposable
    {
        private const int DefaultQueueCapacity = 4096;

        private readonly CanRxDispatcher _owner;
        private readonly Channel<CanFrame> _channel;
        private readonly int _capacity;
        private int _disposed;

        internal Subscription(CanRxDispatcher owner, int capacity = DefaultQueueCapacity)
        {
            _owner = owner;
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _channel = Channel.CreateBounded<CanFrame>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }

        internal bool TryRead(TimeSpan timeout, CancellationToken cancellationToken, out CanFrame frame)
        {
            frame = default;
            if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                return false;

            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            try
            {
                // Another reader may consume an item after WaitToReadAsync
                // completes but before TryRead. Loop until we get a frame or
                // the original timeout expires; never report a false timeout
                // while a concurrent reader merely won the race.
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    var readable = _channel.Reader.WaitToReadAsync(cancellationToken).AsTask();
                    if (!readable.IsCompleted)
                    {
                        var remaining = timeout - stopwatch.Elapsed;
                        if (remaining <= TimeSpan.Zero ||
                            !readable.Wait(remaining, cancellationToken))
                            return false;
                    }

                    if (!readable.GetAwaiter().GetResult())
                        return false;

                    if (_channel.Reader.TryRead(out frame))
                        return true;

                    if (stopwatch.Elapsed >= timeout)
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }

        internal void Publish(in CanFrame frame)
        {
            if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                return;

            // BoundedChannelFullMode.DropOldest atomically evicts the
            // oldest item and publishes the new one. No separate semaphore
            // count can drift from the number of queued frames.
            _channel.Writer.TryWrite(frame);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _owner.Remove(this);
            _channel.Writer.TryComplete();
        }
    }

    private readonly IJ2534Adapter _j2534;
    private readonly object _gate = new();
    private readonly object _lifecycleGate = new();
    private readonly HashSet<Subscription> _subscriptions = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _running;
    private int _disposed;

    internal event Action<Exception>? ReadError;

    internal CanRxDispatcher(IJ2534Adapter j2534)
        => _j2534 = j2534 ?? throw new ArgumentNullException(nameof(j2534));

    internal Subscription Subscribe(int capacity = 4096)
    {
        ObjectDisposedException.ThrowIf(VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed), this);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var subscription = new Subscription(this, capacity);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed), this);
            _subscriptions.Add(subscription);
        }
        return subscription;
    }

    internal void Start()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed), this);
            if (_running != 0)
                return;

            if (_worker is { IsCompleted: false })
                throw new InvalidOperationException("The previous CAN receive worker is still stopping.");

            _worker = null;
            _cts?.Dispose();
            _cts = null;

            var cts = new CancellationTokenSource();
            _cts = cts;
            _running = 1;
            _worker = Task.Run(() => ReadLoop(cts.Token));
        }
    }

    internal void Stop()
    {
        lock (_lifecycleGate)
        {
            // Stop may be called more than once. Even if the running flag was
            // cleared by an earlier timed-out Stop(), still wait for the
            // existing worker before allowing Dispose to tear down resources.
            var worker = _worker;
            var cts = _cts;
            if (_running == 0 && (worker is null || worker.IsCompleted))
                return;

            _running = 0;
            cts?.Cancel();

            try
            {
                worker?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // ReadLoop reports recoverable adapter errors via ReadError.
            }

            if (worker is null || worker.IsCompleted)
            {
                cts?.Dispose();
                _cts = null;
                _worker = null;
                return;
            }

            // A native read can outlive the bounded wait. Keep the worker and
            // CTS alive, and prevent Start() from creating a second reader.
            _ = worker.ContinueWith(
                completed =>
                {
                    cts?.Dispose();
                    lock (_lifecycleGate)
                    {
                        if (ReferenceEquals(_worker, completed))
                        {
                            _worker = null;
                            _cts = null;
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        // Malformed frames can arrive at bus speed. Keep diagnostics useful
        // without flooding the UI/log with one error per bad frame.
        long lastDecodeErrorTimestamp = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_j2534.Read(out var message, 250, out var error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        ReportReadError(new InvalidOperationException(error));

                        // Some adapters return an error immediately instead of
                        // respecting the read timeout. Back off to avoid a hot
                        // loop that can flood the UI/log and consume a CPU core.
                        if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100)))
                            break;
                    }

                    continue;
                }

                if (!CanDecoder.TryDecode(message, out var frame, out error))
                {
                    var now = System.Diagnostics.Stopwatch.GetTimestamp();
                    var elapsedTicks = now - lastDecodeErrorTimestamp;
                    if (lastDecodeErrorTimestamp == 0 ||
                        elapsedTicks >= System.Diagnostics.Stopwatch.Frequency)
                    {
                        lastDecodeErrorTimestamp = now;
                        ReportReadError(new InvalidOperationException("CAN decode error: " + error));
                    }
                    continue;
                }

                Subscription[] subscribers;
                lock (_gate)
                    subscribers = _subscriptions.ToArray();

                foreach (var subscriber in subscribers)
                    subscriber.Publish(frame);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Native adapter failures should be visible to the UI, but a
                // transient exception must not silently kill the receive task.
                ReportReadError(ex);

                // Avoid a hot loop if the adapter keeps throwing immediately.
                if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100)))
                    break;
            }
        }
    }

    private void ReportReadError(Exception error)
    {
        var handlers = ReadError;
        if (handlers is null)
            return;

        foreach (Action<Exception> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(error);
            }
            catch
            {
                // A UI/logging subscriber must not terminate the CAN read loop.
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
            _subscriptions.Remove(subscription);
    }

    private static bool IsDisposed(int value) => value != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Stop();

        // Dispose is the final lifetime barrier for the native J2534 reader.
        // Stop() has a bounded wait for normal UI responsiveness, but unloading
        // the adapter while ReadLoop is still inside a native call is unsafe.
        // Keep the native session alive until that worker has actually exited.
        Task? worker;
        lock (_lifecycleGate)
            worker = _worker;

        try
        {
            worker?.GetAwaiter().GetResult();
        }
        catch
        {
            // The worker's read errors are reported through ReadError; cleanup
            // must still proceed after the task has definitively completed.
        }

        Subscription[] subscribers;
        lock (_gate)
        {
            subscribers = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscriber in subscribers)
            subscriber.Dispose();
    }
}

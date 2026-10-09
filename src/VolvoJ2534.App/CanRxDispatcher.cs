namespace VolvoJ2534.App;

internal sealed class CanRxDispatcher : IDisposable
{
    internal sealed class Subscription : IDisposable
    {
        private const int DefaultQueueCapacity = 4096;

        private readonly CanRxDispatcher _owner;
        private readonly object _gate = new();
        private readonly Queue<CanFrame> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly int _capacity;
        private int _disposed;

        internal Subscription(CanRxDispatcher owner, int capacity = DefaultQueueCapacity)
        {
            _owner = owner;
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
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
                if (!_signal.Wait(timeout, cancellationToken))
                    return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            lock (_gate)
            {
                if (_queue.Count == 0)
                    return false;

                frame = _queue.Dequeue();
                return true;
            }
        }

        internal void Publish(in CanFrame frame)
        {
            if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                return;

            lock (_gate)
            {
                if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                    return;

                if (_queue.Count >= _capacity)
                {
                    // Drop the oldest frame under sustained bus load. Keep the
                    // semaphore count aligned with the queue by consuming the
                    // permit belonging to the dropped item.
                    _queue.Dequeue();
                    _signal.Wait(0);
                }

                _queue.Enqueue(frame);
                try
                {
                    _signal.Release();
                }
                catch (ObjectDisposedException)
                {
                    _queue.Dequeue();
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _owner.Remove(this);
            lock (_gate)
                _queue.Clear();

            _signal.Dispose();
        }
    }

    private readonly J2534Native _j2534;
    private readonly object _gate = new();
    private readonly object _lifecycleGate = new();
    private readonly HashSet<Subscription> _subscriptions = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _running;
    private int _disposed;

    internal event Action<Exception>? ReadError;

    internal CanRxDispatcher(J2534Native j2534)
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_j2534.Read(out var message, 250, out var error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                        ReportReadError(new InvalidOperationException(error));
                    continue;
                }

                if (!CanDecoder.TryDecode(message, out var frame, out error))
                {
                    ReportReadError(new InvalidOperationException("CAN decode error: " + error));
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

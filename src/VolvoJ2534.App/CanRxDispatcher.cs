using System.Collections.Concurrent;

namespace VolvoJ2534.App;

internal sealed class CanRxDispatcher : IDisposable
{
    internal sealed class Subscription : IDisposable
    {
        private readonly CanRxDispatcher _owner;
        private readonly ConcurrentQueue<CanFrame> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private int _disposed;

        internal Subscription(CanRxDispatcher owner) => _owner = owner;

        internal bool TryRead(TimeSpan timeout, CancellationToken cancellationToken, out CanFrame frame)
        {
            frame = default;
            if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                return false;

            while (true)
            {
                if (_queue.TryDequeue(out frame))
                    return true;

                if (!_signal.Wait(timeout, cancellationToken))
                    return false;

                timeout = TimeSpan.Zero;
                if (_queue.TryDequeue(out frame))
                    return true;
            }
        }

        internal void Publish(in CanFrame frame)
        {
            if (VolvoJ2534.App.CanRxDispatcher.IsDisposed(_disposed))
                return;

            _queue.Enqueue(frame);
            try { _signal.Release(); }
            catch (ObjectDisposedException) { _queue.TryDequeue(out _); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _owner.Remove(this);
            _signal.Dispose();
        }
    }

    private readonly J2534Native _j2534;
    private readonly object _gate = new();
    private readonly HashSet<Subscription> _subscriptions = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _running;

    internal event Action<Exception>? ReadError;

    internal CanRxDispatcher(J2534Native j2534)
        => _j2534 = j2534 ?? throw new ArgumentNullException(nameof(j2534));

    internal Subscription Subscribe()
    {
        var subscription = new Subscription(this);
        lock (_gate)
            _subscriptions.Add(subscription);
        return subscription;
    }

    internal void Start()
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
            return;

        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => ReadLoop(_cts.Token));
    }

    internal void Stop()
    {
        if (Interlocked.Exchange(ref _running, 0) == 0)
            return;

        _cts?.Cancel();
        try { _worker?.Wait(TimeSpan.FromSeconds(1)); }
        catch (AggregateException) { }

        _cts?.Dispose();
        _cts = null;
        _worker = null;
    }

    private void ReadLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_j2534.Read(out var message, 250, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                    ReadError?.Invoke(new InvalidOperationException(error));
                continue;
            }

            if (!CanDecoder.TryDecode(message, out var frame, out error))
            {
                ReadError?.Invoke(new InvalidOperationException("CAN decode error: " + error));
                continue;
            }

            Subscription[] subscribers;
            lock (_gate)
                subscribers = _subscriptions.ToArray();

            foreach (var subscriber in subscribers)
                subscriber.Publish(frame);
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

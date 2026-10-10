using System.Collections.Concurrent;

namespace VolvoJ2534.App.Tests;

/// <summary>
/// In-memory J2534 adapter used to exercise the real CAN/ISO-TP stack without
/// loading a vendor DLL or requiring a physical interface.
/// </summary>
internal sealed class VirtualJ2534Adapter : VolvoJ2534.App.IJ2534Adapter, IDisposable
{
    private readonly ConcurrentQueue<VolvoJ2534.App.J2534Native.PassthruMsg> _rx = new();
    private readonly SemaphoreSlim _rxReady = new(0);
    private readonly Func<VolvoJ2534.App.CanFrame, IEnumerable<VolvoJ2534.App.CanFrame>> _onTransmit;
    private int _disposed;
    private int _transmittedFrames;

    internal VirtualJ2534Adapter(
        Func<VolvoJ2534.App.CanFrame, IEnumerable<VolvoJ2534.App.CanFrame>> onTransmit)
        => _onTransmit = onTransmit ?? throw new ArgumentNullException(nameof(onTransmit));

    internal int TransmittedFrames => Volatile.Read(ref _transmittedFrames);

    public bool Load(string path, out string error) { error = string.Empty; return true; }
    public bool Open(out string error) { error = string.Empty; return true; }
    public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }

    public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        msg = default;

        if (!_rx.TryDequeue(out msg))
        {
            if (Volatile.Read(ref _disposed) != 0 || !_rxReady.Wait((int)Math.Min(timeout, int.MaxValue)))
                return false;

            if (!_rx.TryDequeue(out msg))
                return false;
        }

        return true;
    }

    public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        if (!VolvoJ2534.App.CanDecoder.TryDecode(msg, out var frame, out error))
            return false;

        Interlocked.Increment(ref _transmittedFrames);
        foreach (var response in _onTransmit(frame))
        {
            _rx.Enqueue(VolvoJ2534.App.CanDecoder.Encode(
                response.ArbitrationId, response.Data, response.IsExtended));
            _rxReady.Release();
        }

        return true;
    }

    public void Unload() { }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _rxReady.Dispose();
    }
}

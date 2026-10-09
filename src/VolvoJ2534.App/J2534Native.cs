using System.Runtime.InteropServices;

namespace VolvoJ2534.App;

internal sealed unsafe class J2534Native : IDisposable
{
    private const uint StatusNoError = 0;
    private const uint ErrTimeout = 0x00000009;
    internal const uint ProtocolCan = 5;
    internal const int MaxDataSize = 4128;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PassthruMsg
    {
        public uint ProtocolID;
        public uint RxStatus;
        public uint TxFlags;
        public uint Timestamp;
        public uint DataSize;
        public uint ExtraDataIndex;
        public fixed byte Data[MaxDataSize];
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruOpen(IntPtr name, out uint deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruClose(uint deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruConnect(uint deviceId, uint protocolId, uint flags, uint baudRate, out uint channelId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruDisconnect(uint channelId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruReadMsgs(uint channelId, PassthruMsg* msg, ref uint numMsgs, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruWriteMsgs(uint channelId, PassthruMsg* msg, ref uint numMsgs, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruGetLastError([Out] byte[] text);

    private IntPtr _dll;
    private uint _device;
    private uint _channel;
    private PassThruOpen? _open;
    private PassThruClose? _close;
    private PassThruConnect? _connect;
    private PassThruDisconnect? _disconnect;
    private PassThruReadMsgs? _read;
    private PassThruWriteMsgs? _write;
    private PassThruGetLastError? _lastError;

    internal bool Load(string path, out string error)
    {
        error = string.Empty;
        try
        {
            // Re-loading this wrapper must not overwrite a live module handle.
            Unload();

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("J2534 DLL path is empty.");

            _dll = NativeLibrary.Load(path);
            _open = Get<PassThruOpen>("PassThruOpen");
            _close = Get<PassThruClose>("PassThruClose");
            _connect = Get<PassThruConnect>("PassThruConnect");
            _disconnect = Get<PassThruDisconnect>("PassThruDisconnect");
            _read = Get<PassThruReadMsgs>("PassThruReadMsgs");
            _write = Get<PassThruWriteMsgs>("PassThruWriteMsgs");
            _lastError = Get<PassThruGetLastError>("PassThruGetLastError");

            if (_open is null || _close is null || _connect is null ||
                _disconnect is null || _read is null || _write is null)
                throw new InvalidOperationException("DLL does not expose the required J2534 functions.");

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Unload();
            return false;
        }
    }

    private T? Get<T>(string name) where T : Delegate =>
        NativeLibrary.TryGetExport(_dll, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;

    internal bool Open(out string error)
    {
        error = string.Empty;
        if (_open is null) { error = "J2534 DLL is not loaded."; return false; }
        if (_device != 0)
        {
            error = "J2534 device is already open.";
            return false;
        }

        var result = _open(IntPtr.Zero, out _device);
        if (result != StatusNoError)
        {
            _device = 0;
            error = FormatFailure("PassThruOpen", result);
            return false;
        }
        if (_device == 0)
        {
            error = "PassThruOpen succeeded but returned an invalid device ID (0).";
            return false;
        }
        return true;
    }

    internal bool Connect(uint baudRate, out string error)
    {
        error = string.Empty;
        if (_connect is null || _device == 0)
        {
            error = "J2534 device is not open.";
            return false;
        }
        if (_channel != 0)
        {
            error = "J2534 CAN channel is already connected.";
            return false;
        }

        var result = _connect(_device, ProtocolCan, 0, baudRate, out _channel);
        if (result != StatusNoError)
        {
            _channel = 0;
            error = FormatFailure("PassThruConnect", result);
            return false;
        }
        if (_channel == 0)
        {
            error = "PassThruConnect succeeded but returned an invalid channel ID (0).";
            return false;
        }
        return true;
    }

    internal bool Read(out PassthruMsg msg, uint timeout, out string error)
    {
        msg = default;
        error = string.Empty;
        if (_read is null || _channel == 0)
        {
            error = "J2534 CAN channel is not connected.";
            return false;
        }

        uint count = 1;
        fixed (PassthruMsg* pMsg = &msg)
        {
            var result = _read(_channel, pMsg, ref count, timeout);
            if (result == StatusNoError)
            {
                if (count == 1) return true;
                if (count == 0) return false; // No frame is not a driver error.
                error = "PassThruReadMsgs returned success with unexpected message count " + count + ".";
                return false;
            }

            if (result == ErrTimeout) return false;
            error = FormatFailure("PassThruReadMsgs", result);
            return false;
        }
    }

    internal bool Write(in PassthruMsg msg, uint timeout, out string error)
    {
        error = string.Empty;
        if (_write is null || _channel == 0)
        {
            error = "J2534 CAN channel is not connected.";
            return false;
        }

        var copy = msg;
        uint count = 1;
        var result = _write(_channel, &copy, ref count, timeout);
        if (result == StatusNoError && count == 1) return true;
        if (result == StatusNoError)
        {
            error = "PassThruWriteMsgs returned success with unexpected message count " + count + ".";
            return false;
        }

        error = FormatFailure("PassThruWriteMsgs", result);
        return false;
    }

    internal void Disconnect()
    {
        if (_disconnect is not null && _channel != 0)
        {
            _disconnect(_channel);
            _channel = 0;
        }
    }

    internal void Close()
    {
        if (_close is not null && _device != 0)
        {
            _close(_device);
            _device = 0;
        }
    }

    internal void Unload()
    {
        // Attempt every cleanup stage independently. A faulty vendor driver
        // must not prevent Close or leave delegates pointing at an unloaded DLL.
        try { Disconnect(); }
        catch { _channel = 0; }

        try { Close(); }
        catch { _device = 0; }

        _open = null;
        _close = null;
        _connect = null;
        _disconnect = null;
        _read = null;
        _write = null;
        _lastError = null;

        var dll = _dll;
        _dll = IntPtr.Zero;
        if (dll != IntPtr.Zero)
        {
            try { NativeLibrary.Free(dll); }
            catch
            {
                // Cleanup is best-effort; never retain stale function pointers.
            }
        }
    }

    private string FormatFailure(string operation, uint status)
    {
        var detail = LastError();
        return string.IsNullOrWhiteSpace(detail)
            ? operation + " failed with J2534 status 0x" + status.ToString("X8") + "."
            : operation + " failed (J2534 status 0x" + status.ToString("X8") + "): " + detail;
    }

    private string LastError()
    {
        if (_lastError is null) return string.Empty;

        try
        {
            var buffer = new byte[256];
            var result = _lastError(buffer);
            if (result != StatusNoError) return string.Empty;
            var length = Array.IndexOf(buffer, (byte)0);
            if (length < 0) length = buffer.Length;
            return System.Text.Encoding.ASCII.GetString(buffer, 0, length).Trim();
        }
        catch
        {
            // A broken optional diagnostic export must not hide the original
            // J2534 status code or prevent session cleanup.
            return string.Empty;
        }
    }

    public void Dispose()
    {
        Unload();
        GC.SuppressFinalize(this);
    }
}
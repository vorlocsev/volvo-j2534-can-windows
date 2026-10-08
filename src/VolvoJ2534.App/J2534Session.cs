namespace VolvoJ2534.App;

internal sealed class J2534Session : IDisposable
{
    private readonly J2534Native _j2534 = new();
    private CanBus? _bus;

    internal event Action<Exception>? ReadError;

    internal bool IsConnected => _bus is not null;

    internal CanBus Bus =>
        _bus ?? throw new InvalidOperationException("J2534 CAN session is not connected.");

    internal bool Connect(string dllPath, uint baudRate, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(dllPath))
        {
            error = "J2534 DLL path is empty.";
            return false;
        }

        Disconnect();

        if (!_j2534.Load(dllPath.Trim(), out error) ||
            !_j2534.Open(out error) ||
            !_j2534.Connect(baudRate, out error))
        {
            _j2534.Unload();
            return false;
        }

        try
        {
            _bus = new CanBus(_j2534);
            _bus.ReadError += HandleReadError;
            _bus.Start();
            return true;
        }
        catch (Exception ex)
        {
            _j2534.Unload();
            error = ex.Message;
            return false;
        }
    }

    internal void Disconnect()
    {
        if (_bus is not null)
        {
            _bus.ReadError -= HandleReadError;
            _bus.Dispose();
            _bus = null;
        }
        _j2534.Unload();
    }

    private void HandleReadError(Exception error) => ReadError?.Invoke(error);

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}

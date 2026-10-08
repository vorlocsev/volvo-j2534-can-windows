namespace VolvoJ2534.App;

internal enum UdsService : byte
{
    ReadDataByIdentifier = 0x22,
    ReadDtcInformation = 0x19
}

internal sealed class UdsNegativeResponseException : Exception
{
    internal byte RequestedService { get; }
    internal byte NegativeResponseCode { get; }

    internal UdsNegativeResponseException(byte requestedService, byte negativeResponseCode)
        : base($"ECU rejected UDS service 0x{requestedService:X2}: NRC 0x{negativeResponseCode:X2}.")
    {
        RequestedService = requestedService;
        NegativeResponseCode = negativeResponseCode;
    }
}

internal sealed class UdsClient : IDisposable
{
    private readonly IsoTpChannel _channel;
    private int _disposed;

    internal UdsClient(IsoTpChannel channel)
        => _channel = channel ?? throw new ArgumentNullException(nameof(channel));

    internal byte[] ReadDataByIdentifier(
        ushort dataIdentifier,
        CancellationToken cancellationToken = default)
    {
        Span<byte> parameters = stackalloc byte[2];
        parameters[0] = (byte)(dataIdentifier >> 8);
        parameters[1] = (byte)dataIdentifier;
        return Request((byte)UdsService.ReadDataByIdentifier, parameters, cancellationToken);
    }

    internal byte[] ReadDtcByStatusMask(
        byte statusMask = 0xFF,
        CancellationToken cancellationToken = default)
    {
        Span<byte> parameters = stackalloc byte[2];
        parameters[0] = 0x02; // reportDTCByStatusMask
        parameters[1] = statusMask;
        return Request((byte)UdsService.ReadDtcInformation, parameters, cancellationToken);
    }

    internal string ReadVin(CancellationToken cancellationToken = default)
    {
        var data = ReadDataByIdentifier(0xF190, cancellationToken);
        return System.Text.Encoding.ASCII.GetString(data).Trim('\0', ' ', '\r', '\n');
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _channel.Dispose();
    }

    private byte[] Request(
        byte service,
        ReadOnlySpan<byte> parameters,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(UdsClient));

        var request = new byte[1 + parameters.Length];
        request[0] = service;
        parameters.CopyTo(request.AsSpan(1));

        var response = _channel.Request(request, cancellationToken);
        return ParsePositiveResponse(service, response);
    }

    private static byte[] ParsePositiveResponse(byte requestedService, byte[] response)
    {
        if (response.Length == 0)
            throw new InvalidOperationException("UDS ECU returned an empty response.");

        if (response[0] == 0x7F)
        {
            if (response.Length < 3)
                throw new InvalidOperationException("Malformed UDS negative response.");

            if (response[1] != requestedService)
                throw new InvalidOperationException(
                    $"UDS negative response refers to service 0x{response[1]:X2}, expected 0x{requestedService:X2}.");

            throw new UdsNegativeResponseException(response[1], response[2]);
        }

        var expectedService = (byte)(requestedService + 0x40);
        if (response[0] != expectedService)
            throw new InvalidOperationException(
                $"Unexpected UDS response SID 0x{response[0]:X2}; expected 0x{expectedService:X2}.");

        return response.AsSpan(1).ToArray();
    }
}

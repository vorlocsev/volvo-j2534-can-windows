namespace VolvoJ2534.App;

internal enum UdsService : byte
{
    ReadDataByIdentifier = 0x22,
    ReadDtcInformation = 0x19
}

internal sealed record UdsDtcRecord(uint Code, byte Status);

internal sealed class UdsNegativeResponseException : Exception
{
    internal byte RequestedService { get; }
    internal byte NegativeResponseCode { get; }

    internal UdsNegativeResponseException(byte requestedService, byte negativeResponseCode)
        : base($"ECU rejected UDS service 0x{requestedService:X2}: NRC 0x{negativeResponseCode:X2} ({DescribeNegativeResponseCode(negativeResponseCode)}).")
    {
        RequestedService = requestedService;
        NegativeResponseCode = negativeResponseCode;
    }

    internal static string DescribeNegativeResponseCode(byte code) => code switch
    {
        0x10 => "general reject",
        0x11 => "service not supported",
        0x12 => "subfunction not supported",
        0x13 => "incorrect message length or invalid format",
        0x14 => "response too long",
        0x21 => "busy; repeat the request later",
        0x22 => "conditions not correct",
        0x24 => "request sequence error",
        0x25 => "no response from subnet component",
        0x26 => "failure prevents execution",
        0x31 => "request out of range or unsupported data identifier",
        0x33 => "security access denied",
        0x35 => "invalid key",
        0x36 => "exceeded number of attempts",
        0x37 => "required time delay not expired",
        0x78 => "response pending",
        0x7E => "subfunction not supported in active session",
        0x7F => "service not supported in active session",
        _ => "unrecognized or manufacturer-specific negative response code"
    };
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

    internal IReadOnlyList<UdsDtcRecord> ReadDtcByStatusMask(
        byte statusMask = 0xFF,
        CancellationToken cancellationToken = default)
    {
        Span<byte> parameters = stackalloc byte[2];
        parameters[0] = 0x02;
        parameters[1] = statusMask;

        var response = Request((byte)UdsService.ReadDtcInformation, parameters, cancellationToken);
        return ParseDtcResponse(response);
    }

    internal string ReadVin(CancellationToken cancellationToken = default)
    {
        var data = ReadDataByIdentifier(0xF190, cancellationToken);
        return ParseVinData(data);
    }

    internal static string ParseVinData(byte[] data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        // ReadDataByIdentifier returns the DID before the DID-specific data.
        if (data.Length < 2 || data[0] != 0xF1 || data[1] != 0x90)
            throw new InvalidOperationException("UDS VIN response does not contain DID 0xF190.");

        const int vinLength = 17;
        if (data.Length != 2 + vinLength)
            throw new InvalidOperationException(
                $"UDS VIN data must contain exactly {vinLength} characters after DID 0xF190.");

        var vin = System.Text.Encoding.ASCII.GetString(data, 2, vinLength);
        if (vin.Any(ch => !(ch is >= 'A' and <= 'H' or >= 'J' and <= 'N' or >= 'P' and <= 'P' or >= 'R' and <= 'Z' or >= '0' and <= '9')))
            throw new InvalidOperationException("UDS VIN contains characters that are not permitted in a VIN.");

        return vin;
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

        var response = _channel.Request(
            request,
            candidate => IsResponsePending(service, candidate) ||
                         IsUnrelatedResponse(service, parameters, candidate),
            cancellationToken);
        return ParsePositiveResponse(service, response);
    }

    // ISO-TP only identifies the CAN conversation; it does not identify a UDS
    // transaction. Ignore complete payloads that clearly belong to another
    // service or request parameter, rather than failing the current request.
    internal static bool IsUnrelatedResponse(byte requestedService, ReadOnlySpan<byte> parameters, byte[] response)
    {
        if (response is null || response.Length == 0)
            return false; // Let the normal parser report malformed responses.

        if (response[0] == 0x7F)
            return response.Length >= 2 && response[1] != requestedService;

        var expectedService = (byte)(requestedService + 0x40);
        if (response[0] != expectedService)
            return true;

        if (requestedService == (byte)UdsService.ReadDataByIdentifier && parameters.Length >= 2 && response.Length >= 3)
            return response[1] != parameters[0] || response[2] != parameters[1];

        if (requestedService == (byte)UdsService.ReadDtcInformation && parameters.Length >= 1 && response.Length >= 2)
            return response[1] != parameters[0];

        return false;
    }

    internal static bool IsResponsePending(byte requestedService, byte[] response)
        => response is { Length: >= 3 } &&
           response[0] == 0x7F &&
           response[1] == requestedService &&
           response[2] == 0x78;

    internal static byte[] ParsePositiveResponse(byte requestedService, byte[] response)
    {
        if (response is null)
            throw new ArgumentNullException(nameof(response));
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

    internal static IReadOnlyList<UdsDtcRecord> ParseDtcResponse(byte[] response)
    {
        if (response is null)
            throw new ArgumentNullException(nameof(response));
        // Positive response data for ReadDTCInformation (0x19), subfunction
        // 0x02 is: subfunction, DTCStatusAvailabilityMask, DTCFormatIdentifier,
        // followed by zero or more 3-byte DTC + 1-byte status records.
        if (response.Length == 0)
            throw new InvalidOperationException("Malformed UDS DTC response: subfunction is missing.");
        if (response[0] != 0x02)
            throw new InvalidOperationException(
                $"Unexpected DTC subfunction 0x{response[0]:X2}; expected 0x02.");
        if (response.Length < 3)
            throw new InvalidOperationException("Malformed UDS DTC response: header is incomplete.");

        var records = new List<UdsDtcRecord>();
        var offset = 3;

        if ((response.Length - offset) % 4 != 0)
            throw new InvalidOperationException("Malformed UDS DTC record length.");

        while (offset < response.Length)
        {
            var code = (uint)(response[offset] << 16 |
                              response[offset + 1] << 8 |
                              response[offset + 2]);
            records.Add(new UdsDtcRecord(code, response[offset + 3]));
            offset += 4;
        }

        return records;
    }
}

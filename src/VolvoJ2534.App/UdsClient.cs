namespace VolvoJ2534.App;

internal enum UdsService : byte
{
    DiagnosticSessionControl = 0x10,
    ReadDataByIdentifier = 0x22,
    ReadDtcInformation = 0x19,
    TesterPresent = 0x3E
}

internal sealed record UdsResponse(
    byte ServiceId,
    byte[] Payload);

internal sealed record UdsNegativeResponse(
    byte RequestedService,
    byte NegativeResponseCode,
    byte[] Payload);

internal sealed class UdsNegativeResponseException : Exception
{
    internal byte RequestedService { get; }
    internal byte NegativeResponseCode { get; }

    internal UdsNegativeResponseException(
        byte requestedService,
        byte negativeResponseCode)
        : base($"ECU rejected UDS service 0x{requestedService:X2}: NRC 0x{negativeResponseCode:X2}.")
    {
        RequestedService = requestedService;
        NegativeResponseCode = negativeResponseCode;
    }
}

internal sealed class UdsClient
{
    private readonly IsoTpChannel _channel;

    internal UdsClient(IsoTpChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    internal byte[] Request(
        byte service,
        ReadOnlySpan<byte> parameters,
        CancellationToken cancellationToken = default)
    {
        var request = new byte[1 + parameters.Length];
        request[0] = service;
        parameters.CopyTo(request.AsSpan(1));

        var response = _channel.Request(request, cancellationToken);
        return ParsePositiveResponse(service, response);
    }

    internal byte[] ReadDataByIdentifier(
        ushort dataIdentifier,
        CancellationToken cancellationToken = default)
    {
        Span<byte> parameters = stackalloc byte[2];
        parameters[0] = (byte)(dataIdentifier >> 8);
        parameters[1] = (byte)dataIdentifier;

        return Request((byte)UdsService.ReadDataByIdentifier, parameters, cancellationToken);
    }

    internal byte[] ReadDtcInformation(
        byte subFunction,
        ReadOnlySpan<byte> parameters = default,
        CancellationToken cancellationToken = default)
    {
        var requestParameters = new byte[1 + parameters.Length];
        requestParameters[0] = subFunction;
        parameters.CopyTo(requestParameters.AsSpan(1));

        return Request((byte)UdsService.ReadDtcInformation, requestParameters, cancellationToken);
    }

    internal byte[] DiagnosticSessionControl(
        byte sessionType,
        CancellationToken cancellationToken = default)
    {
        return Request(
            (byte)UdsService.DiagnosticSessionControl,
            new[] { sessionType },
            cancellationToken);
    }

    internal byte[] TesterPresent(
        bool suppressPositiveResponse = false,
        CancellationToken cancellationToken = default)
    {
        return Request(
            (byte)UdsService.TesterPresent,
            new[] { (byte)(suppressPositiveResponse ? 0x80 : 0x00) },
            cancellationToken);
    }

    private static byte[] ParsePositiveResponse(byte requestedService, byte[] response)
    {
        if (response.Length == 0)
            throw new InvalidOperationException("UDS ECU returned an empty response.");

        if (response[0] == 0x7F)
        {
            if (response.Length < 3)
                throw new InvalidOperationException("Malformed UDS negative response.");

            var requested = response[1];
            var nrc = response[2];

            if (requested != requestedService)
                throw new InvalidOperationException(
                    $"UDS negative response refers to service 0x{requested:X2}, expected 0x{requestedService:X2}.");

            throw new UdsNegativeResponseException(requested, nrc);
        }

        var expectedService = (byte)(requestedService + 0x40);
        if (response[0] != expectedService)
            throw new InvalidOperationException(
                $"Unexpected UDS response SID 0x{response[0]:X2}; expected 0x{expectedService:X2}.");

        return response.AsSpan(1).ToArray();
    }
}

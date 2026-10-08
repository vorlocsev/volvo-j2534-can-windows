namespace VolvoJ2534.App;

internal sealed record UdsEcuCandidate(
    uint ResponseId,
    uint? RequestId,
    bool IsExtended,
    int ResponseCount,
    int? MaxDataLength);

internal sealed class UdsEcuDiscovery
{
    private readonly CanBus _bus;

    internal UdsEcuDiscovery(CanBus bus)
        => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    internal Task<IReadOnlyList<UdsEcuCandidate>> ScanAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        using var subscription = _bus.Subscribe();
        var observations = new Dictionary<(uint Id, bool Extended), Observation>();
        var end = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = end - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            if (!subscription.TryRead(
                    TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(1, remaining.TotalMilliseconds))),
                    cancellationToken,
                    out var frame))
                continue;

            // Passive only: no probe or diagnostic request is transmitted.
            if (!IsLikelyUdsResponse(frame, out _))
                continue;

            var key = (frame.ArbitrationId, frame.IsExtended);
            if (!observations.TryGetValue(key, out var observation))
            {
                observation = new Observation(frame.ArbitrationId, frame.IsExtended);
                observations.Add(key, observation);
            }

            observation.Count++;
            observation.MaxLength = Math.Max(observation.MaxLength, frame.Data.Length);
        }

        return Task.FromResult<IReadOnlyList<UdsEcuCandidate>>(observations.Values
            .OrderByDescending(x => x.Count)
            .Select(x => new UdsEcuCandidate(
                x.Id,
                GuessRequestId(x.Id, x.IsExtended),
                x.IsExtended,
                x.Count,
                x.MaxLength))
            .ToArray());
    }

    internal static bool IsLikelyUdsResponse(CanFrame frame, out byte? responseService)
    {
        responseService = null;

        if (frame.IsRemote || frame.Data.Length == 0)
            return false;

        if (!IsoTp.TryDecode(frame, out var iso, out _))
            return false;

        if (iso.Data.Length == 0)
            return false;

        var sid = iso.Data[0];

        // UDS negative response.
        if (sid == 0x7F)
        {
            responseService = sid;
            return true;
        }

        // Positive response SID = request SID + 0x40.
        // Current read-only client uses the standard 0x10..0x3E/0x22/0x19
        // service range, whose positive SIDs are in 0x50..0x7E.
        if (sid is >= 0x41 and <= 0x7E)
        {
            responseService = sid;
            return true;
        }

        return false;
    }

    private static uint? GuessRequestId(uint responseId, bool extended)
    {
        if (!extended && responseId is >= 0x7E8 and <= 0x7EF)
            return responseId - 8;

        // ISO 15765-4 fixed-normal 29-bit physical response:
        //   response 0x18DAF1xx -> request 0x18DAxxF1.
        // Do not invent a request ID for other 29-bit schemes.
        if (extended &&
            (responseId & 0x1FFFFF00) == 0x18DAF100)
        {
            var ecuAddress = responseId & 0xFF;
            return 0x18DA0000u | (ecuAddress << 8) | 0xF1;
        }

        return null;
    }

    private sealed class Observation
    {
        internal Observation(uint id, bool extended)
        {
            Id = id;
            IsExtended = extended;
        }

        internal uint Id { get; }
        internal bool IsExtended { get; }
        internal int Count { get; set; }
        internal int MaxLength { get; set; }
    }
}

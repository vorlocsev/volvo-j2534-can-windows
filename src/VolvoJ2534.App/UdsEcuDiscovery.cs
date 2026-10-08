namespace VolvoJ2534.App;

internal sealed record UdsEcuCandidate(
    uint ResponseId,
    uint RequestId,
    bool IsExtended,
    int ResponseCount,
    int? MaxDataLength);

internal sealed class UdsEcuDiscovery
{
    private readonly CanBus _bus;

    internal UdsEcuDiscovery(CanBus bus)
        => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    internal async Task<IReadOnlyList<UdsEcuCandidate>> ScanAsync(
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
            var remaining = end - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            if (!subscription.TryRead(
                    TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(1, remaining.TotalMilliseconds))),
                    cancellationToken,
                    out var frame))
                continue;

            // Passive discovery only: do not transmit probe frames.
            // Standard UDS physical response IDs are commonly 0x7E8..0x7EF.
            if (!IsLikelyUdsResponse(frame))
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

        return observations.Values
            .OrderByDescending(x => x.Count)
            .Select(x => new UdsEcuCandidate(
                x.Id,
                GuessRequestId(x.Id, x.IsExtended),
                x.IsExtended,
                x.Count,
                x.MaxLength))
            .ToArray();
    }

    private static bool IsLikelyUdsResponse(CanFrame frame)
    {
        if (frame.IsRemote || frame.Data.Length == 0)
            return false;

        if (!frame.IsExtended)
            return frame.ArbitrationId is >= 0x7E8 and <= 0x7EF;

        return false;
    }

    private static uint GuessRequestId(uint responseId, bool extended)
        => !extended && responseId is >= 0x7E8 and <= 0x7EF
            ? responseId - 8
            : responseId;

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

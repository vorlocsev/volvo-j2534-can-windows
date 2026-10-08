namespace VolvoJ2534.App;

internal static unsafe class CanDecoder
{
    // J2534 CAN layout: 4-byte arbitration ID followed by the CAN payload.
    internal const uint Can29BitId = 0x00000100;
    private const int HeaderSize = 4;
    private const int MaxClassicCanData = 8;

    internal static bool TryDecode(in J2534Native.PassthruMsg message, out CanFrame frame, out string error)
    {
        frame = default;
        error = string.Empty;

        if (message.ProtocolID != J2534Native.ProtocolCan)
        {
            error = $"Unsupported protocol ID: {message.ProtocolID}.";
            return false;
        }

        if (message.DataSize < HeaderSize)
        {
            error = $"Invalid J2534 CAN message size: {message.DataSize}.";
            return false;
        }

        var available = (int)Math.Min(message.DataSize, (uint)J2534Native.MaxDataSize);
        uint id = ((uint)message.Data[0] << 24) |
                  ((uint)message.Data[1] << 16) |
                  ((uint)message.Data[2] << 8) |
                  message.Data[3];

        var extended = (message.RxStatus & Can29BitId) != 0 ||
                       (message.TxFlags & Can29BitId) != 0 ||
                       id > 0x7FF;

        id = extended ? id & 0x1FFFFFFF : id & 0x7FF;

        var payloadLength = Math.Min(available - HeaderSize, MaxClassicCanData);
        var payload = new byte[payloadLength];
        for (var i = 0; i < payloadLength; i++)
            payload[i] = message.Data[HeaderSize + i];

        frame = new CanFrame(id, extended, false, payload, message.Timestamp, message.RxStatus);
        return true;
    }

    internal static J2534Native.PassthruMsg Encode(uint arbitrationId, ReadOnlySpan<byte> data, bool extended = false)
    {
        if ((!extended && arbitrationId > 0x7FF) || (extended && arbitrationId > 0x1FFFFFFF))
            throw new ArgumentOutOfRangeException(nameof(arbitrationId));
        if (data.Length > MaxClassicCanData)
            throw new ArgumentOutOfRangeException(nameof(data), "Classic CAN payload is limited to 8 bytes.");

        var message = new J2534Native.PassthruMsg
        {
            ProtocolID = J2534Native.ProtocolCan,
            TxFlags = extended ? Can29BitId : 0,
            DataSize = HeaderSize + (uint)data.Length
        };

        message.Data[0] = (byte)(arbitrationId >> 24);
        message.Data[1] = (byte)(arbitrationId >> 16);
        message.Data[2] = (byte)(arbitrationId >> 8);
        message.Data[3] = (byte)arbitrationId;

        for (var i = 0; i < data.Length; i++)
            message.Data[HeaderSize + i] = data[i];

        return message;
    }
}

namespace VolvoJ2534.App;

internal static unsafe class CanDecoder
{
    // J2534 CAN layout: 4-byte arbitration ID followed by the CAN payload.
    // CAN_29BIT_ID (0x100) is the J2534 flag that selects 29-bit CAN IDs.
    internal const uint Can29BitId = 0x00000100;
    private const int HeaderSize = 4;
    private const int MaxClassicCanData = 8;
    private const int MaxCanMessageSize = HeaderSize + MaxClassicCanData;

    internal static bool TryDecode(in J2534Native.PassthruMsg message, out CanFrame frame, out string error)
    {
        frame = default;
        error = string.Empty;

        if (message.ProtocolID != J2534Native.ProtocolCan)
        {
            error = $"Unsupported protocol ID: {message.ProtocolID}.";
            return false;
        }

        if (message.DataSize < HeaderSize || message.DataSize > MaxCanMessageSize)
        {
            error = $"Invalid J2534 CAN message size: {message.DataSize}; expected {HeaderSize}..{MaxCanMessageSize}.";
            return false;
        }

        var id = ((uint)message.Data[0] << 24) |
                 ((uint)message.Data[1] << 16) |
                 ((uint)message.Data[2] << 8) |
                 message.Data[3];

        // RxStatus describes a received frame. TxFlags are transmit
        // options and must not change how a received arbitration ID is decoded.
        var extended = (message.RxStatus & Can29BitId) != 0;

        // Do not silently reinterpret an invalid 11-bit message as 29-bit.
        // J2534 requires the CAN_29BIT_ID flag to describe the identifier type.
        if (!extended && id > 0x7FF)
        {
            error = $"11-bit J2534 CAN message contains ID 0x{id:X}; CAN_29BIT_ID is not set.";
            return false;
        }

        if (extended && id > 0x1FFFFFFF)
        {
            error = $"29-bit J2534 CAN message contains invalid ID 0x{id:X8}.";
            return false;
        }

        var payloadLength = (int)message.DataSize - HeaderSize;
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

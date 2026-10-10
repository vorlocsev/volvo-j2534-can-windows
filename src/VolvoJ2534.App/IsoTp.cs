namespace VolvoJ2534.App;

internal enum IsoTpFrameType : byte
{
    SingleFrame = 0,
    FirstFrame = 1,
    ConsecutiveFrame = 2,
    FlowControl = 3
}

internal enum IsoTpFlowStatus : byte
{
    ContinueToSend = 0,
    Wait = 1,
    Overflow = 2
}

internal readonly record struct IsoTpFrame(
    IsoTpFrameType Type,
    byte[] Data,
    byte SequenceNumber = 0,
    IsoTpFlowStatus FlowStatus = IsoTpFlowStatus.ContinueToSend,
    byte BlockSize = 0,
    byte SeparationTime = 0);

internal static class IsoTp
{
    internal const int MaxPayloadLength = 4095;

    internal static bool TryDecode(CanFrame frame, out IsoTpFrame result, out string error)
    {
        result = default;
        error = string.Empty;
        if (frame.IsRemote || frame.Data.Length == 0)
        {
            error = "CAN frame has no ISO-TP payload.";
            return false;
        }

        // This implementation uses Classic CAN, whose data field is at most
        // eight bytes. CanFrame can also be constructed directly by callers,
        // so do not rely solely on the J2534 decoder to enforce this limit.
        if (frame.Data.Length > 8)
        {
            error = "Classic CAN frame payload exceeds 8 bytes.";
            return false;
        }

        var pci = (byte)(frame.Data[0] >> 4);
        switch ((IsoTpFrameType)pci)
        {
            case IsoTpFrameType.SingleFrame:
            {
                var length = frame.Data[0] & 0x0F;
                if (length == 0 || length > 7 || length > frame.Data.Length - 1)
                {
                    error = "Invalid ISO-TP Single Frame length.";
                    return false;
                }
                result = new IsoTpFrame(IsoTpFrameType.SingleFrame, frame.Data[1..(1 + length)]);
                return true;
            }

            case IsoTpFrameType.FirstFrame:
            {
                // First Frames are used only for payloads larger than a
                // Single Frame and must carry six initial payload bytes in
                // Classic CAN (8-byte CAN data field). Accepting a truncated
                // First Frame would shift the reassembled payload silently.
                if (frame.Data.Length != 8)
                {
                    error = $"Invalid ISO-TP First Frame length: expected 8 CAN data bytes, got {frame.Data.Length}.";
                    return false;
                }

                var length = ((frame.Data[0] & 0x0F) << 8) | frame.Data[1];
                if (length < 8 || length > MaxPayloadLength)
                {
                    error = $"Unsupported ISO-TP payload length: {length}.";
                    return false;
                }

                var firstLength = Math.Min(length, frame.Data.Length - 2);
                result = new IsoTpFrame(IsoTpFrameType.FirstFrame, frame.Data[2..(2 + firstLength)]);
                return true;
            }

            case IsoTpFrameType.ConsecutiveFrame:
            {
                if (frame.Data.Length < 2)
                {
                    error = "Invalid ISO-TP Consecutive Frame: payload bytes are missing.";
                    return false;
                }

                result = new IsoTpFrame(
                    IsoTpFrameType.ConsecutiveFrame,
                    frame.Data.Length > 1 ? frame.Data[1..] : Array.Empty<byte>(),
                    (byte)(frame.Data[0] & 0x0F));
                return true;
            }

            case IsoTpFrameType.FlowControl:
            {
                if (frame.Data.Length < 3)
                {
                    error = "Invalid ISO-TP Flow Control frame.";
                    return false;
                }

                var flowStatus = (byte)(frame.Data[0] & 0x0F);
                if (flowStatus > (byte)IsoTpFlowStatus.Overflow)
                {
                    error = $"Invalid ISO-TP Flow Status: {flowStatus}.";
                    return false;
                }

                result = new IsoTpFrame(
                    IsoTpFrameType.FlowControl,
                    Array.Empty<byte>(),
                    FlowStatus: (IsoTpFlowStatus)flowStatus,
                    BlockSize: frame.Data[1],
                    SeparationTime: frame.Data[2]);
                return true;
            }

            default:
                error = $"Unknown ISO-TP PCI type: {pci}.";
                return false;
        }
    }

    internal static IReadOnlyList<byte[]> Segment(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0 || payload.Length > MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload), $"ISO-TP payload must be 1..{MaxPayloadLength} bytes.");

        var frames = new List<byte[]>();

        if (payload.Length <= 7)
        {
            var sf = new byte[8];
            sf[0] = (byte)payload.Length;
            payload.CopyTo(sf.AsSpan(1));
            frames.Add(sf);
            return frames;
        }

        var ff = new byte[8];
        ff[0] = (byte)(0x10 | ((payload.Length >> 8) & 0x0F));
        ff[1] = (byte)payload.Length;
        payload[..6].CopyTo(ff.AsSpan(2));
        frames.Add(ff);

        var offset = 6;
        byte sequence = 1;
        while (offset < payload.Length)
        {
            var cf = new byte[8];
            cf[0] = (byte)(0x20 | sequence);
            var count = Math.Min(7, payload.Length - offset);
            payload.Slice(offset, count).CopyTo(cf.AsSpan(1));
            frames.Add(cf);
            offset += count;
            sequence = (byte)((sequence + 1) & 0x0F);
        }

        return frames;
    }

    internal static byte[] CreateFlowControl(byte status = 0, byte blockSize = 0, byte separationTime = 0)
    {
        if (status > (byte)IsoTpFlowStatus.Overflow)
            throw new ArgumentOutOfRangeException(nameof(status), "ISO-TP Flow Status must be 0, 1, or 2.");

        return new[] { (byte)(0x30 | status), blockSize, separationTime, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0 };
    }
}

internal sealed class IsoTpReassembler
{
    private readonly List<byte> _buffer = new();
    private int _expectedLength = -1;
    private byte _nextSequence;

    internal bool InProgress => _expectedLength >= 0;
    internal int ExpectedLength => _expectedLength;
    internal int ReceivedLength => _buffer.Count;

    internal bool Push(CanFrame frame, out byte[]? payload, out string error)
    {
        payload = null;
        error = string.Empty;

        if (!IsoTp.TryDecode(frame, out var iso, out error))
        {
            // A malformed frame breaks any in-flight multi-frame message.
            Reset();
            return false;
        }

        switch (iso.Type)
        {
            case IsoTpFrameType.SingleFrame:
                Reset();
                payload = iso.Data;
                return true;

            case IsoTpFrameType.FirstFrame:
                _buffer.Clear();
                _expectedLength = ReadExpectedLength(frame);
                _nextSequence = 1;
                _buffer.AddRange(iso.Data);

                if (_buffer.Count > _expectedLength)
                {
                    error = "ISO-TP First Frame exceeds declared payload length.";
                    Reset();
                    return false;
                }

                if (_buffer.Count == _expectedLength)
                {
                    payload = _buffer.ToArray();
                    Reset();
                }
                return true;

            case IsoTpFrameType.ConsecutiveFrame:
                if (!InProgress)
                {
                    error = "ISO-TP Consecutive Frame received without First Frame.";
                    return false;
                }

                if (iso.SequenceNumber != _nextSequence)
                {
                    error = $"ISO-TP sequence mismatch: expected {_nextSequence}, got {iso.SequenceNumber}.";
                    Reset();
                    return false;
                }

                // Every non-final Consecutive Frame must carry all 7 data
                // bytes; the final frame must carry at least the remaining
                // payload bytes. Padding is permitted only beyond that minimum.
                // Accepting a short frame here would shift subsequent bytes and
                // silently return a corrupted payload.
                var remaining = _expectedLength - _buffer.Count;
                var requiredBytes = Math.Min(7, remaining);
                if (iso.Data.Length < requiredBytes)
                {
                    error = $"ISO-TP Consecutive Frame is too short: expected at least {requiredBytes} data bytes, got {iso.Data.Length}.";
                    Reset();
                    return false;
                }

                var bytesToAppend = Math.Min(remaining, iso.Data.Length);
                _buffer.AddRange(iso.Data.AsSpan(0, bytesToAppend).ToArray());
                _nextSequence = (byte)((_nextSequence + 1) & 0x0F);

                if (_buffer.Count >= _expectedLength)
                {
                    if (_buffer.Count > _expectedLength)
                    {
                        error = "ISO-TP payload contains excess bytes.";
                        Reset();
                        return false;
                    }

                    payload = _buffer.ToArray();
                    Reset();
                }
                return true;

            case IsoTpFrameType.FlowControl:
                error = "Flow Control is not a reassembled payload.";
                Reset();
                return false;

            default:
                error = "Unsupported ISO-TP frame type.";
                Reset();
                return false;
        }
    }

    private static int ReadExpectedLength(CanFrame frame)
        => ((frame.Data[0] & 0x0F) << 8) | frame.Data[1];

    internal void Reset()
    {
        _buffer.Clear();
        _expectedLength = -1;
        _nextSequence = 0;
    }
}

using System.Globalization;

namespace VolvoJ2534.App;

internal readonly record struct CanFrame(
    uint ArbitrationId,
    bool IsExtended,
    bool IsRemote,
    byte[] Data,
    uint Timestamp,
    uint RxStatus)
{
    internal string IdHex =>
        IsExtended ? ArbitrationId.ToString("X8", CultureInfo.InvariantCulture)
                   : ArbitrationId.ToString("X3", CultureInfo.InvariantCulture);

    internal byte Dlc => (byte)Math.Min(Data.Length, 8);
    internal string DataHex => BitConverter.ToString(Data).Replace('-', ' ');
}

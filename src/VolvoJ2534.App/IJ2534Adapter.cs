namespace VolvoJ2534.App;

internal interface IJ2534Adapter
{
    bool Load(string path, out string error);
    bool Open(out string error);
    bool Connect(uint baudRate, out string error);
    bool Read(out J2534Native.PassthruMsg msg, uint timeout, out string error);
    bool Write(in J2534Native.PassthruMsg msg, uint timeout, out string error);
    void Unload();
}

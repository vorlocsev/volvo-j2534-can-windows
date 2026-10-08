using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VolvoJ2534.App;

internal static class J2534Native
{
    private const uint STATUS_NOERROR=0, ERR_TIMEOUT=0x00000009, CAN=5;
    [StructLayout(LayoutKind.Sequential, Pack=1)] internal unsafe struct PassthruMsg { public uint ProtocolID, RxStatus, TxFlags, Timestamp, DataSize, ExtraDataIndex; public fixed byte Data[4128]; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruOpen(IntPtr p, out uint id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruClose(uint id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruConnect(uint id,uint protocol,uint flags,uint baud,out uint channel);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruDisconnect(uint channel);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private unsafe delegate uint PassThruReadMsgs(uint channel, PassthruMsg* msg, ref uint count, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private unsafe delegate uint PassThruWriteMsgs(uint channel, PassthruMsg* msg, ref uint count, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint PassThruGetLastError([Out] byte[] text);
    private IntPtr _dll; private uint _device,_channel; private PassThruOpen? _open; private PassThruClose? _close; private PassThruConnect? _connect; private PassThruDisconnect? _disconnect; private PassThruReadMsgs? _read; private PassThruWriteMsgs? _write; private PassThruGetLastError? _lastError;
    internal bool Load(string path,out string error){error="";try{_dll=NativeLibrary.Load(path);_open=Get<PassThruOpen>("PassThruOpen");_close=Get<PassThruClose>("PassThruClose");_connect=Get<PassThruConnect>("PassThruConnect");_disconnect=Get<PassThruDisconnect>("PassThruDisconnect");_read=Get<PassThruReadMsgs>("PassThruReadMsgs");_write=Get<PassThruWriteMsgs>("PassThruWriteMsgs");_lastError=Get<PassThruGetLastError>("PassThruGetLastError");if(_open==null||_close==null||_connect==null||_disconnect==null||_read==null||_write==null)throw new InvalidOperationException("DLL does not expose the required J2534 functions.");return true;}catch(Exception ex){error=ex.Message;Unload();return false;}}
    private T? Get<T>(string name) where T:Delegate {return NativeLibrary.TryGetExport(_dll,name,out var p)?Marshal.GetDelegateForFunctionPointer<T>(p):null;}
    internal bool Open(out string error){error="";if(_open==null){error="J2534 DLL is not loaded.";return false;}var r=_open(IntPtr.Zero,out _device);if(r!=STATUS_NOERROR){error=LastError();return false;}return true;}
    internal bool Connect(uint baud,out string error){error="";if(_connect==null||_device==0){error="J2534 device is not open.";return false;}var r=_connect(_device,CAN,0,baud,out _channel);if(r!=STATUS_NOERROR){error=LastError();return false;}return true;}
    internal unsafe bool Read(out PassthruMsg msg,uint timeout,out string error){msg=default;error="";if(_read==null||_channel==0)return false;uint n=1;var r=_read(_channel,&msg,ref n,timeout);if(r==STATUS_NOERROR&&n==1)return true;if(r==ERR_TIMEOUT)return false;error=LastError();return false;}
    private string LastError(){if(_lastError==null)return "J2534 operation failed";var b=new byte[256];_lastError(b);var z=Array.IndexOf(b,(byte)0);return System.Text.Encoding.ASCII.GetString(b,0,z<0?b.Length:z);}
    internal void Disconnect(){if(_disconnect!=null&&_channel!=0){_disconnect(_channel);_channel=0;}}
    internal void Close(){if(_close!=null&&_device!=0){_close(_device);_device=0;}}
    internal void Unload(){Disconnect();Close();if(_dll!=IntPtr.Zero){NativeLibrary.Free(_dll);_dll=IntPtr.Zero;}}
}

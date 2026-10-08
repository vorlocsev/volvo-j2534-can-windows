#include "j2534.hpp"
#include <windows.h>
J2534::J2534()=default;
J2534::~J2534(){disconnect();close();if(dll_)FreeLibrary((HMODULE)dll_);}
bool J2534::load(const std::string&p,std::string&e){HMODULE h=LoadLibraryA(p.c_str());if(!h){e="LoadLibrary failed: "+std::to_string(GetLastError());return false;}dll_=h;
open_=(PassThruOpenFn)GetProcAddress(h,"PassThruOpen");close_=(PassThruCloseFn)GetProcAddress(h,"PassThruClose");connect_=(PassThruConnectFn)GetProcAddress(h,"PassThruConnect");disconnect_=(PassThruDisconnectFn)GetProcAddress(h,"PassThruDisconnect");read_=(PassThruReadMsgsFn)GetProcAddress(h,"PassThruReadMsgs");write_=(PassThruWriteMsgsFn)GetProcAddress(h,"PassThruWriteMsgs");getLastError_=(PassThruGetLastErrorFn)GetProcAddress(h,"PassThruGetLastError");
if(!open_||!close_||!connect_||!disconnect_||!read_||!write_){e="DLL does not expose required J2534 functions.";FreeLibrary(h);dll_=nullptr;return false;}return true;}
bool J2534::open(std::string&e){if(!open_){e="DLL not loaded";return false;}auto r=open_(nullptr,&device_);if(r){e=lastError();return false;}return true;}
bool J2534::connectCan(TUInt32 b,std::string&e){if(!connect_||!device_){e="Device not open";return false;}auto r=connect_(device_,CAN,0,b,&channel_);if(r){e=lastError();return false;}return true;}
bool J2534::read(PASSTHRU_MSG&m,TUInt32 t,std::string&e){TUInt32 n=1;auto r=read_(channel_,&m,&n,t);if(r==STATUS_NOERROR&&n==1)return true;if(r==ERR_TIMEOUT)return false;e=lastError();return false;}
bool J2534::write(const PASSTHRU_MSG&i,std::string&e){PASSTHRU_MSG m=i;TUInt32 n=1;auto r=write_(channel_,&m,&n,1000);if(r||n!=1){e=lastError();return false;}return true;}
void J2534::disconnect(){if(disconnect_&&channel_){disconnect_(channel_);channel_=0;}}
void J2534::close(){if(close_&&device_){close_(device_);device_=0;}}
std::string J2534::lastError()const{if(!getLastError_)return"J2534 operation failed";char b[256]{};getLastError_(b);return b[0]?b:"J2534 operation failed";}
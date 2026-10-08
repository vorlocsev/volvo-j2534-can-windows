#pragma once
#include "j2534_defs.hpp"
#include <string>
class J2534 {
public:
 J2534(); ~J2534();
 bool load(const std::string&,std::string&); bool open(std::string&); bool connectCan(TUInt32,std::string&);
 bool read(PASSTHRU_MSG&,TUInt32,std::string&); bool write(const PASSTHRU_MSG&,std::string&);
 void disconnect(); void close(); std::string lastError() const;
private:
 void* dll_=nullptr; J2534_DEVICE_ID device_=0; J2534_CHANNEL_ID channel_=0;
 PassThruOpenFn open_=nullptr; PassThruCloseFn close_=nullptr; PassThruConnectFn connect_=nullptr;
 PassThruDisconnectFn disconnect_=nullptr; PassThruReadMsgsFn read_=nullptr; PassThruWriteMsgsFn write_=nullptr;
 PassThruGetLastErrorFn getLastError_=nullptr;
};
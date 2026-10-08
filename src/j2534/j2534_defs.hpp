#pragma once
#include <cstdint>
using TUInt32=std::uint32_t; using TUInt64=std::uint64_t; using TByte=std::uint8_t;
using J2534_STATUS=TUInt32; using J2534_DEVICE_ID=TUInt32; using J2534_CHANNEL_ID=TUInt32; using J2534_FILTER_ID=TUInt32;
constexpr J2534_STATUS STATUS_NOERROR=0; constexpr J2534_STATUS ERR_TIMEOUT=0x00000009;
constexpr TUInt32 CAN=5; constexpr TUInt32 CAN_500K=500000; constexpr TUInt32 CAN_250K=250000; constexpr TUInt32 CAN_125K=125000;
#pragma pack(push,1)
struct PASSTHRU_MSG { TUInt32 ProtocolID; TUInt32 RxStatus; TUInt32 TxFlags; TUInt32 Timestamp; TUInt32 DataSize; TUInt32 ExtraDataIndex; TByte Data[4128]; };
#pragma pack(pop)
using PassThruOpenFn=J2534_STATUS(__stdcall*)(void*,J2534_DEVICE_ID*);
using PassThruCloseFn=J2534_STATUS(__stdcall*)(J2534_DEVICE_ID);
using PassThruConnectFn=J2534_STATUS(__stdcall*)(J2534_DEVICE_ID,TUInt32,TUInt32,TUInt32,J2534_CHANNEL_ID*);
using PassThruDisconnectFn=J2534_STATUS(__stdcall*)(J2534_CHANNEL_ID);
using PassThruReadMsgsFn=J2534_STATUS(__stdcall*)(J2534_CHANNEL_ID,PASSTHRU_MSG*,TUInt32*,TUInt32);
using PassThruWriteMsgsFn=J2534_STATUS(__stdcall*)(J2534_CHANNEL_ID,PASSTHRU_MSG*,TUInt32*,TUInt32);
using PassThruGetLastErrorFn=J2534_STATUS(__stdcall*)(char*);

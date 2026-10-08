#include "can_monitor.hpp"
#include <iomanip>
#include <sstream>
std::string formatCanFrame(const PASSTHRU_MSG&m){std::ostringstream o;o<<"proto="<<m.ProtocolID<<" ts="<<m.Timestamp<<" len="<<m.DataSize<<" data=";for(TUInt32 i=0;i<m.DataSize&&i<64;i++){if(i)o<<' ';o<<std::uppercase<<std::hex<<std::setw(2)<<std::setfill('0')<<(unsigned)m.Data[i];}return o.str();}
#include "j2534/j2534.hpp"
#include "can/can_monitor.hpp"
#include <iostream>
int main(int argc,char**argv){std::cout<<"Volvo J2534 CAN Windows 0.1.0\n";if(argc<2){std::cout<<"Usage: volvo-j2534-can <j2534.dll> [baud]\n";return 0;}J2534 api;std::string e;auto baud=argc>2?(TUInt32)std::stoul(argv[2]):CAN_500K;if(!api.load(argv[1],e)||!api.open(e)||!api.connectCan(baud,e)){std::cerr<<e<<"\n";return 1;}std::cout<<"Connected at "<<baud<<" bit/s. Ctrl+C to stop.\n";for(;;){PASSTHRU_MSG m{};if(api.read(m,1000,e))std::cout<<formatCanFrame(m)<<"\n";}}
#include "gui.hpp"
#include "j2534/j2534.hpp"
#include <windows.h>
#include <commctrl.h>
#include <commdlg.h>
#include <atomic>
#include <fstream>
#include <memory>
#include <thread>
#include <vector>
#include <sstream>
#include <iomanip>
#pragma comment(lib, "comctl32.lib")
namespace {
constexpr int IDC_DLL=1001,IDC_BAUD=1002,IDC_CONNECT=1003,IDC_START=1004,IDC_CLEAR=1005,IDC_FILTER=1006,IDC_LOG=1007,IDC_STATUS=1008,IDC_TABLE=1009,IDC_BROWSE=1010;
constexpr UINT WM_CAN_FRAME=WM_APP+10,WM_CAN_ERROR=WM_APP+11;
struct Frame{DWORD ts{},id{};std::vector<unsigned char> data;};
struct State{HWND hwnd{},dll{},baud{},connect{},start{},filter{},status{},table{},clear{},log{};std::unique_ptr<J2534> api;std::thread reader;std::atomic<bool> stop{false};bool connected=false,monitoring=false;std::ofstream csv;};
State* g=nullptr;
std::string utf8(const std::wstring&s){if(s.empty())return{};int n=WideCharToMultiByte(CP_UTF8,0,s.data(),(int)s.size(),nullptr,0,nullptr,nullptr);std::string r(n,'\\0');WideCharToMultiByte(CP_UTF8,0,s.data(),(int)s.size(),r.data(),n,nullptr,nullptr);return r;}
std::wstring text(HWND h){int n=GetWindowTextLengthW(h);std::wstring s(n,L'\\0');if(n)GetWindowTextW(h,s.data(),n+1);return s;}
void status(const std::wstring&s){SetWindowTextW(g->status,s.c_str());}
void addFrame(const Frame&f){auto flt=text(g->filter);if(!flt.empty()){try{if(f.id!=std::stoul(flt,nullptr,16))return;}catch(...){return;}}
wchar_t a[32],b[32],c[8];swprintf_s(a,L"%u",f.ts);swprintf_s(b,L"%03X",f.id);swprintf_s(c,L"%u",(unsigned)f.data.size());std::wstring d;
for(size_t i=0;i<f.data.size();++i){wchar_t x[4];swprintf_s(x,L"%02X",f.data[i]);if(i)d+=L' ';d+=x;}
int row=ListView_GetItemCount(g->table);LVITEMW it{};it.mask=LVIF_TEXT;it.iItem=row;it.pszText=a;ListView_InsertItem(g->table,&it);ListView_SetItemText(g->table,row,1,b);ListView_SetItemText(g->table,row,2,c);ListView_SetItemText(g->table,row,3,d.data());if(row>5000)ListView_DeleteItem(g->table,0);
if(g->csv.is_open()){g->csv<<f.ts<<","<<std::hex<<std::uppercase<<f.id<<std::dec<<","<<f.data.size()<<",\"";for(size_t i=0;i<f.data.size();++i){if(i)g->csv<<' ';g->csv<<std::uppercase<<std::hex<<std::setw(2)<<std::setfill('0')<<(int)f.data[i];}g->csv<<"\"\n";g->csv.flush();}}
void stop(){if(!g->monitoring)return;g->stop=true;if(g->reader.joinable())g->reader.join();g->monitoring=false;EnableWindow(g->start,TRUE);SetWindowTextW(g->start,L"Start");}
void loop(){while(!g->stop){PASSTHRU_MSG m{};std::string e;if(g->api->read(m,250,e)){Frame f{m.Timestamp,m.ProtocolID,{}};if(m.DataSize)f.data.assign(m.Data,m.Data+m.DataSize);PostMessageW(g->hwnd,WM_CAN_FRAME,0,(LPARAM)new Frame(std::move(f)));}else if(!e.empty()&&!g->stop){std::wstring w(e.begin(),e.end());PostMessageW(g->hwnd,WM_CAN_ERROR,0,(LPARAM)new std::wstring(L"Read error: "+w));break;}}}
void start(){if(!g->connected||g->monitoring)return;g->stop=false;g->monitoring=true;EnableWindow(g->start,FALSE);status(L"Monitoring CAN...");g->reader=std::thread(loop);}
void browse(){wchar_t p[MAX_PATH]{};OPENFILENAMEW o{sizeof(o)};o.hwndOwner=g->hwnd;o.lpstrFile=p;o.nMaxFile=MAX_PATH;o.lpstrFilter=L"J2534 DLL (*.dll)\\0*.dll\\0All files (*.*)\\0*.*\\0";o.Flags=OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST;if(GetOpenFileNameW(&o))SetWindowTextW(g->dll,p);}
void connect(){if(g->connected){stop();g->api->disconnect();g->api->close();g->api.reset();g->connected=false;SetWindowTextW(g->connect,L"Connect");status(L"Disconnected");return;}auto p=utf8(text(g->dll));if(p.empty()){status(L"Select a J2534 DLL first");return;}TUInt32 baud=CAN_500K;int s=(int)SendMessageW(g->baud,CB_GETCURSEL,0,0);if(s==1)baud=CAN_250K;else if(s==2)baud=CAN_125K;g->api=std::make_unique<J2534>();std::string e;if(!g->api->load(p,e)||!g->api->open(e)||!g->api->connectCan(baud,e)){status(std::wstring(L"Connection failed: ")+std::wstring(e.begin(),e.end()));g->api.reset();return;}g->connected=true;SetWindowTextW(g->connect,L"Disconnect");status(L"Connected");}
void csv(){wchar_t p[MAX_PATH]=L"can-log.csv";OPENFILENAMEW o{sizeof(o)};o.hwndOwner=g->hwnd;o.lpstrFile=p;o.nMaxFile=MAX_PATH;o.lpstrFilter=L"CSV files (*.csv)\\0*.csv\\0All files (*.*)\\0*.*\\0";o.Flags=OFN_OVERWRITEPROMPT;if(GetSaveFileNameW(&o)){if(g->csv.is_open())g->csv.close();g->csv.open(utf8(p),std::ios::trunc);if(g->csv){g->csv<<"timestamp,id,dlc,data\\n";status(std::wstring(L"CSV logging: ")+p);}else status(L"Cannot open CSV file");}}
void layout(HWND h){RECT r{};GetClientRect(h,&r);int w=r.right,y=10;SetWindowPos(g->dll,0,10,y,w-300,26,SWP_NOZORDER);SetWindowPos(GetDlgItem(h,IDC_BROWSE),0,w-280,y,80,26,SWP_NOZORDER);SetWindowPos(g->baud,0,w-190,y,85,26,SWP_NOZORDER);SetWindowPos(g->connect,0,w-95,y,85,26,SWP_NOZORDER);y+=38;SetWindowPos(g->start,0,10,y,85,26,SWP_NOZORDER);SetWindowPos(g->clear,0,105,y,85,26,SWP_NOZORDER);SetWindowPos(g->log,0,200,y,100,26,SWP_NOZORDER);SetWindowPos(g->filter,0,380,y,140,26,SWP_NOZORDER);SetWindowPos(g->status,0,530,y,w-540,26,SWP_NOZORDER);y+=38;SetWindowPos(g->table,0,10,y,w-20,r.bottom-y-10,SWP_NOZORDER);}
LRESULT CALLBACK wnd(HWND h,UINT m,WPARAM w,LPARAM l){switch(m){
case WM_CREATE:{g=(State*)((CREATESTRUCTW*)l)->lpCreateParams;g->hwnd=h;g->dll=CreateWindowW(L"EDIT",L"",WS_CHILD|WS_VISIBLE|WS_BORDER|ES_AUTOHSCROLL,0,0,0,0,h,(HMENU)IDC_DLL,0,0);CreateWindowW(L"BUTTON",L"Browse...",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_BROWSE,0,0);g->baud=CreateWindowW(L"COMBOBOX",L"",WS_CHILD|WS_VISIBLE|CBS_DROPDOWNLIST,0,0,0,0,h,(HMENU)IDC_BAUD,0,0);for(auto x:{L"500 kbit/s",L"250 kbit/s",L"125 kbit/s"})SendMessageW(g->baud,CB_ADDSTRING,0,(LPARAM)x);SendMessageW(g->baud,CB_SETCURSEL,0,0);g->connect=CreateWindowW(L"BUTTON",L"Connect",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_CONNECT,0,0);g->start=CreateWindowW(L"BUTTON",L"Start",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_START,0,0);g->clear=CreateWindowW(L"BUTTON",L"Clear",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_CLEAR,0,0);g->log=CreateWindowW(L"BUTTON",L"CSV Log...",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_LOG,0,0);CreateWindowW(L"STATIC",L"ID filter (hex):",WS_CHILD|WS_VISIBLE,305,48,75,22,h,0,0,0);g->filter=CreateWindowW(L"EDIT",L"",WS_CHILD|WS_VISIBLE|WS_BORDER|ES_AUTOHSCROLL,0,0,0,0,h,(HMENU)IDC_FILTER,0,0);g->status=CreateWindowW(L"STATIC",L"Disconnected",WS_CHILD|WS_VISIBLE,0,0,0,0,h,(HMENU)IDC_STATUS,0,0,0);g->table=CreateWindowW(WC_LISTVIEWW,L"",WS_CHILD|WS_VISIBLE|WS_BORDER|LVS_REPORT|LVS_SINGLESEL,0,0,0,0,h,(HMENU)IDC_TABLE,0,0);ListView_SetExtendedListViewStyle(g->table,LVS_EX_FULLROWSELECT|LVS_EX_GRIDLINES|LVS_EX_DOUBLEBUFFER);const wchar_t* n[]={L"Timestamp (ms)",L"ID",L"DLC",L"Data"};int z[]={130,100,60,700};for(int i=0;i<4;i++){LVCOLUMNW c{};c.mask=LVCF_TEXT|LVCF_WIDTH;c.pszText=(LPWSTR)n[i];c.cx=z[i];ListView_InsertColumn(g->table,i,&c);}layout(h);return 0;}
case WM_SIZE:layout(h);return 0;
case WM_COMMAND:switch(LOWORD(w)){case IDC_BROWSE:browse();break;case IDC_CONNECT:connect();break;case IDC_START:start();break;case IDC_CLEAR:ListView_DeleteAllItems(g->table);break;case IDC_LOG:csv();break;}return 0;
case WM_CAN_FRAME:{auto*f=(Frame*)l;addFrame(*f);delete f;return 0;}
case WM_CAN_ERROR:{auto*e=(std::wstring*)l;status(*e);delete e;stop();return 0;}
case WM_DESTROY:stop();if(g->api){g->api->disconnect();g->api->close();g->api.reset();}if(g->csv.is_open())g->csv.close();PostQuitMessage(0);return 0;}return DefWindowProcW(h,m,w,l);}
}
int runGui(){INITCOMMONCONTROLSEX i{sizeof(i),ICC_LISTVIEW_CLASSES};InitCommonControlsEx(&i);WNDCLASSW c{};c.lpfnWndProc=wnd;c.hInstance=GetModuleHandleW(0);c.lpszClassName=L"VolvoJ2534CanWindow";c.hCursor=LoadCursor(0,IDC_ARROW);c.hbrBackground=(HBRUSH)(COLOR_WINDOW+1);RegisterClassW(&c);State s;HWND h=CreateWindowW(c.lpszClassName,L"Volvo J2534 CAN Monitor",WS_OVERLAPPEDWINDOW|WS_CLIPCHILDREN,CW_USEDEFAULT,CW_USEDEFAULT,1100,700,0,0,c.hInstance,&s);ShowWindow(h,SW_SHOW);UpdateWindow(h);MSG m;while(GetMessageW(&m,0,0,0)>0){TranslateMessage(&m);DispatchMessageW(&m);}return(int)m.wParam;}

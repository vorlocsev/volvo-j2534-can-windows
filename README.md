# Volvo J2534 CAN Monitor — WinUI 3 x86

Windows 11 diagnostic/CAN monitor for Volvo interfaces using the SAE J2534 API.

## Supported interfaces
- Volvo DiCE
- Mongoose JLR Pro

Drivers and proprietary DLLs are installed separately.

## Scope
Read-only CAN diagnostics, monitoring and logging. This project does not implement CEM PIN extraction, immobilizer unlocking or PIN brute forcing.

## Build
Requirements: Windows 11, Visual Studio 2022, CMake 3.24+.

```powershell
cmake -S . -B build -A x64
cmake --build build --config Release
```

For legacy 32-bit J2534 DLLs:
```powershell
cmake -S . -B build-x86 -A Win32
cmake --build build-x86 --config Release
```

The application accepts the J2534 DLL path explicitly so it does not assume a proprietary installation layout.


## GUI

The Windows application now starts as a native Win32 GUI.

Features:
- J2534 DLL path with **Browse**
- CAN bitrate: 500 / 250 / 125 kbit/s
- Connect / Disconnect
- Start / Stop CAN monitoring
- live CAN frame table with timestamp, ID, DLC and data
- hexadecimal CAN-ID filter
- clear capture
- CSV logging
- status/error display

### Important: DLL architecture

The application architecture must match the J2534 DLL:
- **x64 build** -> use a 64-bit J2534 DLL
- **Win32 build** -> use a 32-bit J2534 DLL

For older Volvo DiCE installations this can be important because legacy DiCE software may provide a 32-bit DLL.

### Running

After building, launch:

`build\Release\volvo-j2534-can.exe`

Select the vendor J2534 DLL with **Browse**, select the CAN bitrate, then press **Connect** and **Start**.

The application does not bundle or install proprietary DiCE/Mongoose drivers.


The supported application is WinUI 3, .NET 8 and x86-only (32-bit). The app loads only 32-bit vendor J2534 DLLs.

## Scope
Read-only CAN monitoring and CSV logging. No immobilizer/PIN extraction, brute forcing, security bypass, or transmit controls are implemented.

## Build
Requirements: Windows 11, Visual Studio 2022, .NET 8 SDK, Windows App SDK support.

```powershell
dotnet restore .\src\VolvoJ2534.App\VolvoJ2534.App.csproj
dotnet build .\src\VolvoJ2534.App\VolvoJ2534.App.csproj -c Release -p:Platform=x86
dotnet publish .\src\VolvoJ2534.App\VolvoJ2534.App.csproj -c Release -r win-x86 --self-contained true
```

## UI
J2534 DLL selection, CAN speed 500/250/125 kbit/s, Connect/Disconnect, Start/Stop, live CAN table, hexadecimal ID filter, frame rate, frame count, Clear and CSV export.

## Important
The process is 32-bit. A 64-bit-only J2534 DLL cannot be loaded. The old C++/Win32 source remains only as historical source; the supported application is src/VolvoJ2534.App and the solution is x86-only.

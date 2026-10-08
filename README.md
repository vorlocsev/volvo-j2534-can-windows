# Volvo J2534 CAN Windows

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

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

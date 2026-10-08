# Volvo J2534 CAN Monitor — WinUI 3 x86

Windows diagnostic/CAN monitor for Volvo interfaces using SAE J2534.

## Supported interfaces

- Volvo DiCE
- Mongoose JLR Pro

Drivers and proprietary J2534 DLLs are installed separately.

## Current scope

- J2534 DLL loading
- device open/close
- CAN 500 / 250 / 125 kbit/s
- live CAN capture
- basic filtering
- frame-rate/count statistics
- CSV export

No immobilizer/PIN extraction, brute forcing, security bypass, or automated security-key calculation is implemented.

## Architecture

The supported application is the C# WinUI 3 project under `src/VolvoJ2534.App`.

```
WinUI 3
  ↓
J2534Native
  ↓
Vendor J2534 DLL
  ↓
DiCE / Mongoose
  ↓
CAN
```

There is now one J2534 backend. The old C++/Win32 backend was removed to avoid two independent implementations.

## Build

Requirements:

- Windows 11
- Visual Studio 2022
- .NET 8 SDK
- Windows App SDK 1.8
- x86 build for legacy 32-bit J2534 DLLs

```powershell
dotnet restore .\src\VolvoJ2534.App\VolvoJ2534.App.csproj
dotnet build .\src\VolvoJ2534.App\VolvoJ2534.App.csproj -c Release -p:Platform=x86
dotnet publish .\src\VolvoJ2534.App\VolvoJ2534.App.csproj -c Release -r win-x86 --self-contained true
```

The process is x86, so it can load 32-bit J2534 DLLs but not 64-bit-only DLLs.

## Next architecture steps

1. Correct CAN arbitration-ID decoding.
2. Standard/extended CAN support.
3. J2534 hardware filters.
4. Automatic J2534 device discovery.
5. ISO-TP transport.
6. UDS diagnostic layer.
7. Volvo ECU profiles.

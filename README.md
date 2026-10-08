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


### ISO-TP

The CAN layer now includes `IsoTp.cs` with Single/First/Consecutive/Flow Control parsing, segmentation and reassembly up to 4095 bytes.

### ISO-TP channel

`IsoTpChannel` provides a request/response transport over the J2534 CAN channel:

- configurable request CAN ID and response CAN ID;
- 11-bit or 29-bit CAN IDs;
- Single Frame and multi-frame request/response;
- Flow Control generation for incoming First Frames;
- Flow Control reception for outgoing First Frames;
- Block Size (`BS`) handling in both directions;
- `STmin` handling, including millisecond and 100–900 microsecond encodings;
- sequence-number validation and wrap-around;
- request, Flow Control and Consecutive Frame timeouts;
- `CancellationToken` support;
- bounded handling of repeated Flow Control `WAIT` frames.

Example configuration:

```csharp
var channel = new IsoTpChannel(
    j2534,
    rxDispatcher,
    new IsoTpChannel.Options(
        RequestId: 0x7E0,
        ResponseId: 0x7E8,
        RxBlockSize: 0,
        RxSeparationTime: 0,
        RequestTimeout: TimeSpan.FromSeconds(5)));
```

`IsoTpChannel.Request(...)` is synchronous and expects exclusive ownership of the J2534 receive queue while the transaction is running. A parallel CAN monitor must not consume the same J2534 RX messages during an ISO-TP exchange.

The channel is transport-only. It does not implement UDS security access, PIN extraction, key calculation, or other immobilizer/security bypass operations.
### Shared CAN RX dispatcher

`CanRxDispatcher` is now the single owner of `PassThruReadMsgs`. It continuously reads the J2534 RX queue and fans out decoded `CanFrame` instances to independent subscriptions.

The CAN monitor and `IsoTpChannel` therefore no longer compete for the same J2534 receive queue. An ISO-TP transaction can consume only its own subscribed frames while the monitor receives the same traffic independently.

The dispatcher must be started after `PassThruConnect` and stopped before the J2534 channel is disconnected.
namespace VolvoJ2534.App.Tests;

public sealed class J2534NativeTests
{
    [Fact]
    public void Load_RejectsEmptyPathAndLeavesWrapperUnloaded()
    {
        using var native = new VolvoJ2534.App.J2534Native();

        Assert.False(native.Load("  ", out var loadError));
        Assert.Contains("path is empty", loadError, StringComparison.OrdinalIgnoreCase);

        Assert.False(native.Open(out var openError));
        Assert.Contains("not loaded", openError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsMissingLibraryAndCanBeRetried()
    {
        using var native = new VolvoJ2534.App.J2534Native();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-j2534.dll");

        Assert.False(native.Load(missingPath, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.False(native.Open(out var openError));
        Assert.Contains("not loaded", openError, StringComparison.OrdinalIgnoreCase);

        // A failed load must not poison the wrapper's state.
        Assert.False(native.Load(missingPath, out var retryError));
        Assert.False(string.IsNullOrWhiteSpace(retryError));
    }

    [Fact]
    public void ConnectWithoutLoadedDeviceReturnsDiagnosticInsteadOfThrowing()
    {
        using var native = new VolvoJ2534.App.J2534Native();

        Assert.False(native.Connect(500000, out var error));
        Assert.Contains("not open", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadAndWriteWithoutConnectedChannelReturnDiagnostic()
    {
        using var native = new VolvoJ2534.App.J2534Native();

        Assert.False(native.Read(out _, 100, out var readError));
        Assert.Contains("not connected", readError, StringComparison.OrdinalIgnoreCase);

        var message = default(VolvoJ2534.App.J2534Native.PassthruMsg);
        Assert.False(native.Write(in message, 100, out var writeError));
        Assert.Contains("not connected", writeError, StringComparison.OrdinalIgnoreCase);
    }
}

namespace VolvoJ2534.App.Tests;

public sealed class CanBusTests
{
    [Fact]
    public unsafe void SendEncodesStandardCanFrameAndWritesThroughAdapter()
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        var payload = new byte[] { 0x22, 0xF1, 0x90 };

        Assert.True(bus.Send(
            0x7E0,
            payload,
            extended: false,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            out var error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(1, adapter.WriteCount);
        Assert.Equal(VolvoJ2534.App.J2534Native.ProtocolCan, adapter.LastMessage.ProtocolID);
        Assert.Equal(0u, adapter.LastMessage.TxFlags);
        Assert.Equal(7u, adapter.LastMessage.DataSize);

        var message = adapter.LastMessage;
        byte* data = message.Data;
        Assert.Equal(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x22, 0xF1, 0x90 },
            new byte[] { data[0], data[1], data[2], data[3], data[4], data[5], data[6] });
    }

    [Fact]
    public void SendRejectsClassicCanPayloadLongerThanEightBytes()
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Send(
            0x7E0,
            new byte[9],
            extended: false,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            out _));
        Assert.Equal(0, adapter.WriteCount);
    }

    [Theory]
    [InlineData(0x800u, false)]
    [InlineData(0x20000000u, true)]
    public void SendRejectsOutOfRangeCanIdentifierWithoutWriting(uint arbitrationId, bool extended)
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Send(
            arbitrationId,
            new byte[] { 0x01 },
            extended,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None,
            out _));

        Assert.Equal(0, adapter.WriteCount);
    }

    [Theory]
    [InlineData(0x7FFu, false)]
    [InlineData(0x1FFFFFFFu, true)]
    public void SendAcceptsMaximumCanIdentifier(uint arbitrationId, bool extended)
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        Assert.True(bus.Send(arbitrationId, new byte[] { 0x01 }, extended,
            TimeSpan.FromMilliseconds(100), CancellationToken.None, out var error));

        Assert.Equal(string.Empty, error);
        Assert.Equal(1, adapter.WriteCount);
        Assert.Equal(extended ? VolvoJ2534.App.CanDecoder.Can29BitId : 0u, adapter.LastMessage.TxFlags);
    }

    [Fact]
    public void SendPropagatesAdapterFailureAndError()
    {
        var adapter = new FakeJ2534Adapter { WriteResult = false, WriteError = "adapter write failed" };
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        Assert.False(bus.Send(0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromMilliseconds(100), CancellationToken.None, out var error));

        Assert.Equal("adapter write failed", error);
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public void SendHonorsCancellationBeforeWriting()
    {
        var adapter = new FakeJ2534Adapter();
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 },
            false, TimeSpan.FromMilliseconds(100), cancellation.Token, out _));
        Assert.Equal(0, adapter.WriteCount);
    }

    [Fact]
    public async Task SendTimesOutWhenTransmitLockIsHeld()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var allowWriteToFinish = new ManualResetEventSlim();
        var adapter = new FakeJ2534Adapter
        {
            WriteEntered = writeEntered,
            AllowWriteToFinish = allowWriteToFinish
        };
        using var bus = new VolvoJ2534.App.CanBus(adapter);

        var firstSend = Task.Run(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromSeconds(2), CancellationToken.None, out _));

        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(1)), "First write did not start.");
            Assert.False(bus.Send(0x7E0, new byte[] { 0x3E, 0x00 }, false,
                TimeSpan.FromMilliseconds(30), CancellationToken.None, out var error));
            Assert.Equal("Timed out waiting for the CAN transmit lock.", error);
            Assert.Equal(1, adapter.WriteCount);
        }
        finally
        {
            allowWriteToFinish.Set();
        }

        Assert.True(await firstSend);
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public async Task SendHonorsCancellationWhileWaitingForTransmitLock()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var allowWriteToFinish = new ManualResetEventSlim();
        var adapter = new FakeJ2534Adapter
        {
            WriteEntered = writeEntered,
            AllowWriteToFinish = allowWriteToFinish
        };
        using var bus = new VolvoJ2534.App.CanBus(adapter);
        using var cancellation = new CancellationTokenSource();

        var firstSend = Task.Run(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromSeconds(2), CancellationToken.None, out _));

        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(1)), "First write did not start.");
            var secondSend = Task.Run(() => bus.Send(0x7E0, new byte[] { 0x22, 0xF1, 0x90 }, false,
                TimeSpan.FromSeconds(2), cancellation.Token, out _));

            await Task.Delay(50); // Give the second send time to block on the held transmit lock.
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondSend);
            Assert.Equal(1, adapter.WriteCount);
        }
        finally
        {
            allowWriteToFinish.Set();
        }

        Assert.True(await firstSend);
        Assert.Equal(1, adapter.WriteCount);
    }

    [Fact]
    public void DisposeIsIdempotentAndSendAfterDisposeThrows()
    {
        var adapter = new FakeJ2534Adapter();
        var bus = new VolvoJ2534.App.CanBus(adapter);

        bus.Dispose();
        bus.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 },
            false, TimeSpan.FromMilliseconds(100), CancellationToken.None, out _));
        Assert.Equal(0, adapter.WriteCount);
    }

    [Fact]
    public async Task DisposeDuringActiveSendDoesNotBreakTransmitLockRelease()
    {
        using var writeEntered = new ManualResetEventSlim();
        using var allowWriteToFinish = new ManualResetEventSlim();
        var adapter = new FakeJ2534Adapter
        {
            WriteEntered = writeEntered,
            AllowWriteToFinish = allowWriteToFinish
        };
        var bus = new VolvoJ2534.App.CanBus(adapter);

        var sendTask = Task.Run(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 }, false,
            TimeSpan.FromSeconds(2), CancellationToken.None, out _));

        try
        {
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(1)), "CAN write did not start.");
            bus.Dispose();
        }
        finally
        {
            allowWriteToFinish.Set();
        }

        Assert.True(await sendTask);
        bus.Dispose();
        Assert.Throws<ObjectDisposedException>(() => bus.Send(0x7E0, new byte[] { 0x3E, 0x00 },
            false, TimeSpan.FromMilliseconds(100), CancellationToken.None, out _));
    }

    private sealed class FakeJ2534Adapter : VolvoJ2534.App.IJ2534Adapter
    {
        internal VolvoJ2534.App.J2534Native.PassthruMsg LastMessage { get; private set; }
        internal bool WriteResult { get; init; } = true;
        internal string WriteError { get; init; } = string.Empty;
        internal ManualResetEventSlim? WriteEntered { get; init; }
        internal ManualResetEventSlim? AllowWriteToFinish { get; init; }

        public bool Load(string path, out string error) { error = string.Empty; return true; }
        public bool Open(out string error) { error = string.Empty; return true; }
        public bool Connect(uint baudRate, out string error) { error = string.Empty; return true; }
        public bool Read(out VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            msg = default;
            error = string.Empty;
            Thread.Sleep((int)Math.Min(timeout, 5));
            return false;
        }

        public bool Write(in VolvoJ2534.App.J2534Native.PassthruMsg msg, uint timeout, out string error)
        {
            LastMessage = msg;
            Interlocked.Increment(ref _writeCount);
            WriteEntered?.Set();
            AllowWriteToFinish?.Wait(TimeSpan.FromSeconds(3));
            error = WriteError;
            return WriteResult;
        }

        private int _writeCount;
        internal int WriteCount => Volatile.Read(ref _writeCount);

        public void Unload() { }
    }
}

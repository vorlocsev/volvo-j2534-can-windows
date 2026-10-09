using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace VolvoJ2534.App;

public sealed partial class MainWindow : Window
{
    private sealed class Frame
    {
        public uint Timestamp { get; init; }
        public string Id { get; init; } = "";
        public string Type { get; init; } = "";
        public uint Dlc { get; init; }
        public string Data { get; init; } = "";
    }

    private readonly ObservableCollection<Frame> _frames = new();
    private readonly J2534Session _session = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _connected;
    private int _total;
    private readonly Stopwatch _rate = new();
    private readonly SemaphoreSlim _udsGate = new(1, 1);
    private CancellationTokenSource? _discoveryCts;
    private Task<IReadOnlyList<UdsEcuCandidate>>? _discoveryTask;
    private CancellationTokenSource? _udsCts;
    private readonly ObservableCollection<EcuRow> _ecus = new();
    private int _rateFrames;

    public MainWindow()
    {
        InitializeComponent();
        FramesView.ItemsSource = _frames;
        EcusView.ItemsSource = _ecus;
        _session.ReadError += ex => DispatcherQueue.TryEnqueue(() => SetStatus("Read error: " + ex.Message));
        Closed += (_, _) =>
        {
            // Ask background operations to stop before touching the native J2534
            // session. Do not dispose it while a worker may still be using it.
            _discoveryCts?.Cancel();
            _udsCts?.Cancel();

            var discoveryTask = _discoveryTask;
            var monitorTask = _monitorTask;
            StopMonitor();

            try { discoveryTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            try { monitorTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }

            var udsStopped = _udsGate.Wait(TimeSpan.FromSeconds(2));
            if (udsStopped)
                _udsGate.Release();

            var discoveryStopped = discoveryTask is null || discoveryTask.IsCompleted;
            var monitorStopped = monitorTask is null || monitorTask.IsCompleted;
            if (discoveryStopped && monitorStopped && udsStopped)
                _session.Dispose();
            // If a native adapter call is still blocked, avoid racing Dispose
            // against that call. The process is closing and will reclaim handles.
        };
    }

    private void SetStatus(string text) => StatusText.Text = text;


    private sealed class EcuRow
    {
        public UdsEcuCandidate Candidate { get; }
        public string Response => Candidate.ResponseId.ToString("X3");
        public string Request => Candidate.RequestId?.ToString("X") ?? "-";
        public string Type => Candidate.IsExtended ? "29-bit" : "11-bit";
        public int Frames => Candidate.ResponseCount;
        public string MaxPayload => Candidate.MaxDataLength?.ToString() ?? "-";

        internal EcuRow(UdsEcuCandidate candidate) => Candidate = candidate;
    }

    private async void Discover_Click(object sender, RoutedEventArgs e)
    {
        if (!_connected) return;

        if (_discoveryCts is not null) return;

        var cts = new CancellationTokenSource();
        _discoveryCts = cts;
        DiscoverButton.IsEnabled = false;
        CancelDiscoveryButton.IsEnabled = true;
        ConnectButton.IsEnabled = false;
        DiscoveryStatus.Text = "Scanning...";
        _ecus.Clear();
        EcuSelector.Items.Clear();

        try
        {
            var discovery = new UdsEcuDiscovery(_session.Bus);
            _discoveryTask = discovery.ScanAsync(TimeSpan.FromSeconds(8), cts.Token);
            var found = await _discoveryTask;

            foreach (var candidate in found)
            {
                var row = new EcuRow(candidate);
                _ecus.Add(row);
                EcuSelector.Items.Add($"{row.Request} → {row.Response}");
            }

            if (found.Count > 0)
            {
                EcusView.SelectedIndex = 0;
                EcuSelector.SelectedIndex = 0;
            }

            DiscoveryStatus.Text = found.Count == 0
                ? "No UDS responses observed"
                : $"{found.Count} ECU(s) found";

            SetStatus("ECU discovery complete.");
        }
        catch (OperationCanceledException)
        {
            DiscoveryStatus.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            DiscoveryStatus.Text = "Failed";
            SetStatus("ECU discovery failed: " + ex.Message);
        }
        finally
        {
            _discoveryTask = null;
            _discoveryCts = null;
            cts.Dispose();
            CancelDiscoveryButton.IsEnabled = false;
            DiscoverButton.IsEnabled = _connected;
            ConnectButton.IsEnabled = true;
        }
    }

    private void CancelDiscovery_Click(object sender, RoutedEventArgs e)
    {
        _discoveryCts?.Cancel();
        DiscoveryStatus.Text = "Cancelling...";
        CancelDiscoveryButton.IsEnabled = false;
    }

    private void Ecu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EcusView.SelectedItem is not EcuRow row)
            return;

        ApplyEcuSelection(row, EcusView.SelectedIndex);
    }

    private void EcuSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = EcuSelector.SelectedIndex;
        if (index < 0 || index >= _ecus.Count)
            return;

        EcusView.SelectedIndex = index;
        ApplyEcuSelection(_ecus[index], index);
    }

    private void ApplyEcuSelection(EcuRow row, int index)
    {
        if (row.Candidate.RequestId is uint requestId)
            UdsRequestId.Text = requestId.ToString(row.Candidate.IsExtended ? "X8" : "X3");
        UdsResponseId.Text = row.Candidate.ResponseId.ToString(row.Candidate.IsExtended ? "X8" : "X3");

        if (EcuSelector.SelectedIndex != index)
            EcuSelector.SelectedIndex = index;

        EcuInfoText.Text =
            $"Request: {(row.Candidate.RequestId is uint ? $"0x{row.Candidate.RequestId.Value:X}" : "unknown")}  ·  " +
            $"Response: 0x{row.Candidate.ResponseId:X}  ·  " +
            $"Type: {(row.Candidate.IsExtended ? "29-bit" : "11-bit")}  ·  " +
            $"Observed frames: {row.Candidate.ResponseCount}  ·  " +
            $"Max payload: {row.Candidate.MaxDataLength?.ToString() ?? "-"} bytes";
    }

    private void SetUdsEnabled(bool enabled)
    {
        ReadDidButton.IsEnabled = enabled;
        ReadVinButton.IsEnabled = enabled;
        ReadDtcButton.IsEnabled = enabled;
    }

    private bool TryGetUdsClient(out UdsClient? client, out string error)
    {
        client = null;
        error = string.Empty;

        if (!_connected)
        {
            error = "Connect to J2534 first.";
            return false;
        }

        if (!TryParseCanId(UdsRequestId.Text, out var requestId, out var requestError) ||
            !TryParseCanId(UdsResponseId.Text, out var responseId, out requestError))
        {
            error = requestError;
            return false;
        }

        var canExtendedId = requestId > 0x7FF || responseId > 0x7FF;

        try
        {
            var channel = new IsoTpChannel(
                _session.Bus,
                new IsoTpChannel.Options(requestId, responseId, canExtendedId));

            client = new UdsClient(channel);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseCanId(string text, out uint id, out string error)
    {
        error = string.Empty;
        id = 0;

        var value = text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value) ||
            !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out id) ||
            id > 0x1FFFFFFF)
        {
            error = "CAN ID must be a valid 11-bit or 29-bit hexadecimal value (000-1FFFFFFF).";
            return false;
        }

        return true;
    }

    private async void ReadDid_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDid(out var did)) return;

        await RunUdsOperationAsync("Read DID", (client, token) => Hex(client.ReadDataByIdentifier(did, token)));
    }

    private async void ReadVin_Click(object sender, RoutedEventArgs e)
    {
        await RunUdsOperationAsync("Read VIN", (client, token) => client.ReadVin(token));
    }

    private async void ReadDtc_Click(object sender, RoutedEventArgs e)
    {
        await RunUdsOperationAsync("Read DTC", (client, token) =>
{
    var dtcs = client.ReadDtcByStatusMask(0xFF, token);
    return dtcs.Count == 0
        ? "No DTCs"
        : string.Join(", ", dtcs.Select(d => $"0x{d.Code:X6}/status=0x{d.Status:X2}"));
});
    }

    private static string Hex(byte[] data) => BitConverter.ToString(data).Replace('-', ' ');

    private bool TryParseDid(out ushort did)
    {
        did = 0;
        var value = UdsDid.Text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);

        if (!ushort.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out did))
        {
            SetStatus("DID must be a valid 16-bit hexadecimal value.");
            return false;
        }

        return true;
    }

    private async Task RunUdsOperationAsync(
        string operation,
        Func<UdsClient, CancellationToken, string> action)
    {
        if (!await _udsGate.WaitAsync(0))
        {
            SetStatus("Another UDS request is already running.");
            return;
        }

        try
        {
            if (!TryGetUdsClient(out var client, out var error) || client is null)
            {
                SetStatus(error);
                return;
            }

            using var udsClient = client;
            using var cts = new CancellationTokenSource();
            _udsCts = cts;

            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            CancelUdsButton.IsEnabled = true;
            SetStatus(operation + "...");

            var data = await Task.Run(() => action(client, cts.Token));
            SetStatus(operation + ": " + data);
        }
        catch (OperationCanceledException)
        {
            SetStatus(operation + " cancelled.");
        }
        catch (UdsNegativeResponseException ex)
        {
            SetStatus(operation + " rejected: NRC 0x" + ex.NegativeResponseCode.ToString("X2"));
        }
        catch (TimeoutException ex)
        {
            SetStatus(operation + " timeout: " + ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(operation + " failed: " + ex.Message);
        }
        finally
        {
            _udsCts = null;
            CancelUdsButton.IsEnabled = false;
            SetUdsEnabled(_connected);
            DiscoverButton.IsEnabled = _connected;
            ConnectButton.IsEnabled = true;
            _udsGate.Release();
        }
    }


    private void CancelUds_Click(object sender, RoutedEventArgs e)
    {
        _udsCts?.Cancel();
        CancelUdsButton.IsEnabled = false;
        SetStatus("Cancelling UDS request...");
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_connected)
        {
            if (_discoveryCts is not null || !_udsGate.Wait(0))
            {
                SetStatus("Wait for the active diagnostic operation to finish before disconnecting.");
                return;
            }
            _udsGate.Release();
            StopMonitor(); _session.Disconnect(); _connected = false;
            ConnectButton.Content = "Connect"; StartButton.IsEnabled = false; SetUdsEnabled(false); DiscoverButton.IsEnabled = false; SetStatus("Disconnected");
            return;
        }

        if (string.IsNullOrWhiteSpace(DllPath.Text)) { SetStatus("Select a J2534 DLL."); return; }

        var baud = uint.Parse(((ComboBoxItem)BaudRate.SelectedItem).Tag.ToString()!);
        if (!_session.Connect(DllPath.Text.Trim(), baud, out var error))
        { SetStatus("Connection failed: " + error); return; }

        _connected = true;
        ConnectButton.Content = "Disconnect"; StartButton.IsEnabled = true; SetUdsEnabled(true); DiscoverButton.IsEnabled = true;
        SetStatus("Connected · J2534 · CAN");
        await Task.CompletedTask;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    { if (_cts is null) StartMonitor(); else StopMonitor(); }

    private void StartMonitor()
    {
        if (!_connected || _monitorTask is not null) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var subscription = _session.Bus.Subscribe();

        StartButton.Content = "Stop monitor";
        SetStatus("Monitoring CAN...");
        _rate.Restart();
        _rateFrames = 0;

        _monitorTask = Task.Run(() =>
        {
            try { ReadLoop(subscription, token); }
            finally { subscription.Dispose(); }
        });
    }

    private void StopMonitor()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        StartButton.Content = "Start monitor";

        var task = _monitorTask;
        _monitorTask = null;

        if (task is not null && !task.IsCompleted)
        {
            try { task.Wait(TimeSpan.FromSeconds(1)); }
            catch (AggregateException) { }
        }

        if (cts is not null)
        {
            if (task is null || task.IsCompleted)
                cts.Dispose();
            else
                _ = task.ContinueWith(
                    _ => cts.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        if (_connected) SetStatus("Connected");
    }

    private void ReadLoop(CanRxDispatcher.Subscription subscription, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!subscription.TryRead(TimeSpan.FromMilliseconds(250), token, out var canFrame))
                continue;

            var frame = new Frame
            {
                Timestamp = canFrame.Timestamp,
                Id = canFrame.IdHex,
                Type = canFrame.IsExtended ? "29-bit" : "11-bit",
                Dlc = canFrame.Dlc,
                Data = canFrame.DataHex
            };

            DispatcherQueue.TryEnqueue(() => AddFrame(frame));
        }
    }

    private void AddFrame(Frame frame)
    {
        if (!Matches(frame)) return;
        _frames.Add(frame); _total++; _rateFrames++;
        if (_frames.Count > 5000) _frames.RemoveAt(0);
        FrameCountText.Text = $"{_total:N0} frames";

        if (_rate.ElapsedMilliseconds >= 1000)
        {
            FrameRateText.Text = $"{_rateFrames} frames/s";
            _rateFrames = 0; _rate.Restart();
        }
    }

    private bool Matches(Frame frame)
    {
        var query = IdFilter.Text.Trim().TrimStart('0');
        if (string.IsNullOrEmpty(query)) return true;
        query = query.PadLeft(3, '0');
        return string.Equals(query, frame.Id.PadLeft(3, '0'), StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e) { }

    private void Clear_Click(object sender, RoutedEventArgs e)
    { _frames.Clear(); _total = 0; FrameCountText.Text = "0 frames"; }

    private async void Csv_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "can-log";
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var lines = new List<string> { "timestamp,id,type,dlc,data" };
        lines.AddRange(_frames.Select(frame =>
            $"{frame.Timestamp},{frame.Id},{frame.Type},{frame.Dlc},\"{frame.Data}\""));

        await File.WriteAllLinesAsync(file.Path, lines);
        SetStatus("CSV saved: " + file.Path);
    }
}

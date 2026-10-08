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
        public uint Dlc { get; init; }
        public string Data { get; init; } = "";
    }

    private readonly ObservableCollection<Frame> _frames = new();
    private readonly J2534Native _j = new();
    private CancellationTokenSource? _cts;
    private bool _connected;
    private int _total;
    private readonly Stopwatch _rate = new();
    private int _rateFrames;

    public MainWindow()
    {
        InitializeComponent();
        FramesView.ItemsSource = _frames;
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_connected)
        {
            StopMonitor();
            _j.Unload();
            _connected = false;
            ConnectButton.Content = "Connect";
            StartButton.IsEnabled = false;
            SetStatus("Disconnected");
            return;
        }

        if (string.IsNullOrWhiteSpace(DllPath.Text))
        {
            SetStatus("Select a J2534 DLL.");
            return;
        }

        if (!_j.Load(DllPath.Text.Trim(), out var error) || !_j.Open(out error))
        {
            SetStatus("Connection failed: " + error);
            return;
        }

        var baud = uint.Parse(((ComboBoxItem)BaudRate.SelectedItem).Tag.ToString()!);
        if (!_j.Connect(baud, out error))
        {
            _j.Unload();
            SetStatus("CAN connect failed: " + error);
            return;
        }

        _connected = true;
        ConnectButton.Content = "Disconnect";
        StartButton.IsEnabled = true;
        SetStatus("Connected · J2534 · x86");
        await Task.CompletedTask;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is null) StartMonitor(); else StopMonitor();
    }

    private void StartMonitor()
    {
        if (!_connected) return;
        _cts = new CancellationTokenSource();
        StartButton.Content = "Stop monitor";
        SetStatus("Monitoring CAN...");
        _rate.Restart();
        _rateFrames = 0;
        _ = Task.Run(() => ReadLoop(_cts.Token));
    }

    private void StopMonitor()
    {
        _cts?.Cancel();
        _cts = null;
        StartButton.Content = "Start monitor";
        if (_connected) SetStatus("Connected");
    }

    private unsafe void ReadLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_j.Read(out var message, 250, out var error))
            {
                if (!string.IsNullOrEmpty(error))
                    DispatcherQueue.TryEnqueue(() => SetStatus("Read error: " + error));
                continue;
            }

            var size = (int)Math.Min(message.DataSize, 4128u);
            var bytes = new byte[size];
            for (var i = 0; i < size; i++) bytes[i] = message.Data[i];

            var frame = new Frame
            {
                Timestamp = message.Timestamp,
                Id = "CAN",
                Dlc = message.DataSize,
                Data = BitConverter.ToString(bytes).Replace('-', ' ')
            };

            DispatcherQueue.TryEnqueue(() => AddFrame(frame));
        }
    }

    private void AddFrame(Frame frame)
    {
        if (!Matches(frame)) return;
        _frames.Add(frame);
        _total++;
        _rateFrames++;
        if (_frames.Count > 5000) _frames.RemoveAt(0);
        FrameCountText.Text = $"{_total:N0} frames";

        if (_rate.ElapsedMilliseconds >= 1000)
        {
            FrameRateText.Text = $"{_rateFrames} frames/s";
            _rateFrames = 0;
            _rate.Restart();
        }
    }

    private bool Matches(Frame frame)
    {
        var query = IdFilter.Text.Trim();
        return string.IsNullOrEmpty(query) ||
               string.Equals(query, frame.Id, StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e) { }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _frames.Clear();
        _total = 0;
        FrameCountText.Text = "0 frames";
    }

    private async void Csv_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "can-log";
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var lines = new List<string> { "timestamp,id,dlc,data" };
        lines.AddRange(_frames.Select(frame =>
            $"{frame.Timestamp},{frame.Id},{frame.Dlc},"{frame.Data}""));

        await File.WriteAllLinesAsync(file.Path, lines);
        SetStatus("CSV saved: " + file.Path);
    }

    protected override void OnClosed(ClosedEventArgs args)
    {
        StopMonitor();
        _j.Dispose();
        base.OnClosed(args);
    }
}
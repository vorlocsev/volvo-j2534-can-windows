using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace VolvoJ2534.App;

public sealed partial class MainWindow : Window
{
    private sealed class Frame { public uint Timestamp {get;init;} public string Id {get;init;}=""; public uint Dlc {get;init;} public string Data {get;init;}=""; }
    private readonly ObservableCollection<Frame> _frames=new();
    private readonly J2534Native _j=new(); private CancellationTokenSource? _cts; private bool _connected; private int _total; private readonly Stopwatch _rate=new(); private int _rateFrames;
    public MainWindow(){InitializeComponent();FramesView.ItemsSource=_frames;Title="Volvo J2534 CAN Monitor";}
    private void SetStatus(string s)=>StatusText.Text=s;
    private async void Connect_Click(object sender,RoutedEventArgs e){if(_connected){StopMonitor();_j.Unload();_connected=false;ConnectButton.Content="Connect";StartButton.IsEnabled=false;SetStatus("Disconnected");return;}if(string.IsNullOrWhiteSpace(DllPath.Text)){SetStatus("Select a 32-bit J2534 DLL.");return;}if(!_j.Load(DllPath.Text.Trim(),out var err)||!_j.Open(out err)){SetStatus("Connection failed: "+err);return;}uint baud=uint.Parse(((ComboBoxItem)BaudRate.SelectedItem).Tag.ToString()!);if(!_j.Connect(baud,out err)){_j.Unload();SetStatus("CAN connect failed: "+err);return;}_connected=true;ConnectButton.Content="Disconnect";StartButton.IsEnabled=true;SetStatus("Connected · J2534 · x86");await Task.CompletedTask;}
    private void Start_Click(object sender,RoutedEventArgs e){if(_cts==null)StartMonitor();else StopMonitor();}
    private void StartMonitor(){if(!_connected)return;_cts=new CancellationTokenSource();StartButton.Content="Stop monitor";SetStatus("Monitoring CAN...");_rate.Restart();_rateFrames=0;Task.Run(()=>ReadLoop(_cts.Token));}
    private void StopMonitor(){_cts?.Cancel();_cts=null;StartButton.Content="Start monitor";if(_connected)SetStatus("Connected");}
    private unsafe void ReadLoop(CancellationToken token){while(!token.IsCancellationRequested){if(!_j.Read(out var m,250,out var err)){if(!string.IsNullOrEmpty(err))DispatcherQueue.TryEnqueue(()=>SetStatus("Read error: "+err));continue;}var bytes=new byte[Math.Min(m.DataSize,4128)];fixed(byte* p=bytes){for(int i=0;i<bytes.Length;i++)p[i]=m.Data[i];}var f=new Frame{Timestamp=m.Timestamp,Id=$"{m.ProtocolID:X3}",Dlc=m.DataSize,Data=BitConverter.ToString(bytes).Replace('-',' ')};DispatcherQueue.TryEnqueue(()=>AddFrame(f));}}
    private void AddFrame(Frame f){if(!Matches(f))return;_frames.Add(f);_total++;_rateFrames++;if(_frames.Count>5000)_frames.RemoveAt(0);FrameCountText.Text=$"{_total:N0} frames";if(_rate.ElapsedMilliseconds>=1000){FrameRateText.Text=$"{_rateFrames} frames/s";_rateFrames=0;_rate.Restart();}}
    private bool Matches(Frame f){var q=IdFilter.Text.Trim();return string.IsNullOrEmpty(q)||string.Equals(q,f.Id,StringComparison.OrdinalIgnoreCase)||string.Equals(q.TrimStart('0'),f.Id.TrimStart('0'),StringComparison.OrdinalIgnoreCase);}
    private void Filter_Changed(object sender,Microsoft.UI.Xaml.Controls.TextChangedEventArgs e){}
    private void Clear_Click(object sender,RoutedEventArgs e){_frames.Clear();_total=0;FrameCountText.Text="0 frames";}
    private async void Csv_Click(object sender,RoutedEventArgs e){var picker=new FileSavePicker();InitializeWithWindow.Initialize(picker,WindowNative.GetWindowHandle(this));picker.SuggestedFileName="can-log";picker.FileTypeChoices.Add("CSV",new List<string>{".csv"});var file=await picker.PickSaveFileAsync();if(file==null)return;var lines=new List<string>{"timestamp,id,dlc,data"};lines.AddRange(_frames.Select(f=>$"{f.Timestamp},{f.Id},{f.Dlc},\"{f.Data}\""));await File.WriteAllLinesAsync(file.Path,lines);SetStatus("CSV saved: "+file.Path);}
    ~MainWindow(){_cts?.Cancel();_j.Unload();}
}

using System.Runtime.InteropServices;
using System.Text;
namespace YoWidgets;

internal sealed class AudioRecorder : IDisposable
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<AudioRecorder,byte> activeRecorders=new();
    internal static bool AnyRecording=>activeRecorders.Keys.Any(r=>r.Recording);
    readonly Dispatcher dispatcher;
    readonly WaveCallback callback;
    readonly List<(IntPtr Header, IntPtr Data)> buffers = [];
    readonly MemoryStream audio = new();
    readonly object gate = new();
    IntPtr device;
    volatile bool recording;
    bool heardSpeech;
    long silentSamples;
    int pendingStop;
    int generation;
    bool streaming;
    public bool Recording => recording;
    float level;
    public double Level => Volatile.Read(ref level);
    public string Error { get; private set; } = "";
    public event Action? Changed;
    public event Action<byte[]>? Captured;
    public event Action<byte[]>? PcmCaptured;
    public AudioRecorder(Dispatcher dispatcher) { this.dispatcher = dispatcher; callback = Receive; }
    public void Start()
    { StartCore(false,16000); }
    public void StartStreaming(int rate)
    { if(rate is not 16000 and not 24000)throw new ArgumentOutOfRangeException(nameof(rate));StartCore(true,rate); }
    void StartCore(bool stream,int rate)
    {
        if (recording) return;
        generation++;streaming=stream;
        Error = ""; heardSpeech = false; silentSamples = 0; pendingStop = 0; audio.SetLength(0); Volatile.Write(ref level,0);
        try
        {
            var format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSecond = (uint)rate, AverageBytesPerSecond = (uint)rate*2, BlockAlign = 2, BitsPerSample = 16 };
            Check(waveInOpen(out device,uint.MaxValue,ref format,callback,IntPtr.Zero,0x30000));
            for (int i = 0; i < 3; i++)
            {
                var size=rate/10; // 50 ms of mono PCM16.
                var data = Marshal.AllocHGlobal(size); var header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>()); buffers.Add((header,data));
                Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = (uint)size },header,false);
                Check(waveInPrepareHeader(device,header,(uint)Marshal.SizeOf<WaveHeader>()));
                Check(waveInAddBuffer(device,header,(uint)Marshal.SizeOf<WaveHeader>()));
            }
            recording = true;activeRecorders[this]=0; Check(waveInStart(device));
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        { Error = "Microphone unavailable. Check the default input device and microphone access in Windows."; Release(); }
        Changed?.Invoke();
    }
    void Receive(IntPtr handle,uint message,IntPtr instance,IntPtr header,IntPtr extra)
    {
        if (message != 0x3C0 || !recording) return;
        bool stop;
        lock (gate)
        {
            if (!recording) return;
            var current = Marshal.PtrToStructure<WaveHeader>(header);
            var bytes = new byte[current.BytesRecorded]; Marshal.Copy(current.Data,bytes,0,bytes.Length);
            if(!streaming)audio.Write(bytes);
            else {var version=generation;dispatcher.BeginInvoke(()=>{if(recording&&version==generation)PcmCaptured?.Invoke(bytes);});}
            var peak = 0; for (int i = 0; i + 1 < bytes.Length; i += 2) peak = Math.Max(peak,Math.Abs((int)(short)(bytes[i] | bytes[i+1] << 8)));
            Volatile.Write(ref level,(float)Math.Clamp((peak-180)/6500d,0,1));
            if (peak > 500) { heardSpeech = true; silentSamples = 0; } else silentSamples += bytes.Length / 2;
            stop = !streaming&&(audio.Length >= 960000 || heardSpeech && silentSamples >= 12800);
        }
        if (stop) { var version = generation; if (Interlocked.Exchange(ref pendingStop,1) == 0) dispatcher.BeginInvoke(() => { if (version == generation && recording) Stop(true); }); }
        else lock(gate) { if(recording)waveInAddBuffer(handle,header,(uint)Marshal.SizeOf<WaveHeader>()); }
    }
    public void Stop(bool transcribe)
    {
        if (device == IntPtr.Zero) return;
        generation++;
        recording = false;activeRecorders.TryRemove(this,out _); Release();
        byte[] bytes; lock (gate) bytes = audio.ToArray();
        if (transcribe && !streaming && bytes.Length > 3200 && heardSpeech)
        {
            using var wav = new MemoryStream(); using var writer = new BinaryWriter(wav,Encoding.UTF8,true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+bytes.Length); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes.Length); writer.Write(bytes); writer.Flush();
            Changed?.Invoke(); Captured?.Invoke(wav.ToArray());
        }
        else { if (transcribe) Error = "No speech detected. Try speaking closer to the microphone."; Changed?.Invoke(); }
    }
    void Release()
    {
        recording = false;activeRecorders.TryRemove(this,out _);
        Volatile.Write(ref level,0);
        if (device != IntPtr.Zero)
        {
            waveInStop(device); waveInReset(device);
            foreach (var buffer in buffers) waveInUnprepareHeader(device,buffer.Header,(uint)Marshal.SizeOf<WaveHeader>());
            waveInClose(device); device = IntPtr.Zero;
        }
        lock (gate) { foreach (var buffer in buffers) { Marshal.FreeHGlobal(buffer.Header); Marshal.FreeHGlobal(buffer.Data); } buffers.Clear(); }
    }
    static void Check(uint code) { if (code != 0) throw new InvalidOperationException("Audio device error"); }
    public void Dispose() { Stop(false); audio.Dispose(); }
    delegate void WaveCallback(IntPtr handle,uint message,IntPtr instance,IntPtr header,IntPtr extra);
    [StructLayout(LayoutKind.Sequential,Pack=2)] struct WaveFormat { public ushort FormatTag,Channels; public uint SamplesPerSecond,AverageBytesPerSecond; public ushort BlockAlign,BitsPerSample,ExtraSize; }
    [StructLayout(LayoutKind.Sequential)] struct WaveHeader { public IntPtr Data; public uint BufferLength,BytesRecorded; public UIntPtr User; public uint Flags,Loops; public IntPtr Next; public UIntPtr Reserved; }
    [DllImport("winmm.dll")] static extern uint waveInOpen(out IntPtr handle,uint id,ref WaveFormat format,WaveCallback callback,IntPtr instance,uint flags);
    [DllImport("winmm.dll")] static extern uint waveInPrepareHeader(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInUnprepareHeader(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInAddBuffer(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")] static extern uint waveInStart(IntPtr handle);
    [DllImport("winmm.dll")] static extern uint waveInStop(IntPtr handle);
    [DllImport("winmm.dll")] static extern uint waveInReset(IntPtr handle);
    [DllImport("winmm.dll")] static extern uint waveInClose(IntPtr handle);
}

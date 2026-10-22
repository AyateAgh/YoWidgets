using System.Runtime.InteropServices;
namespace YoWidgets;

// PCM stays in bounded native buffers. Device position, not network arrival time,
// determines how much speech was actually heard when the user interrupts.
internal sealed class PcmSpeaker : IDisposable
{
    readonly Dispatcher dispatcher;
    readonly WaveCallback callback;
    readonly List<Buffer> buffers=[];
    IntPtr device;
    long submitted;
    record Buffer(IntPtr Header,IntPtr Data,int Length,long Start,double Level);
    public PcmSpeaker(Dispatcher dispatcher){this.dispatcher=dispatcher;callback=Receive;}
    internal long PlayedBytes {get{if(device==IntPtr.Zero)return 0;var position=new Position{Type=4};return waveOutGetPosition(device,ref position,(uint)Marshal.SizeOf<Position>())==0&&position.Type==4?position.Value:0;}}
    internal bool Playing=>device!=IntPtr.Zero&&submitted-PlayedBytes>0;
    internal long SubmittedBytes=>submitted;
    internal double Level=>buffers.FirstOrDefault(b=>b.Start+b.Length>PlayedBytes)?.Level??0;
    internal void Open()
    {
        if(device!=IntPtr.Zero)return;
        var format=new Format{Tag=1,Channels=1,Rate=24000,Bytes=48000,Align=2,Bits=16};
        Check(waveOutOpen(out device,uint.MaxValue,ref format,callback,IntPtr.Zero,0x30000));
    }
    internal void Write(byte[] pcm)
    {
        if(pcm.Length==0)return;
        if(pcm.Length%2!=0)throw new InvalidOperationException("The voice service returned invalid audio.");
        Open();Collect();
        if(submitted-PlayedBytes+pcm.Length>48000*30)throw new InvalidOperationException("Voice playback fell behind. Start a new call.");
        var data=Marshal.AllocHGlobal(pcm.Length);var header=Marshal.AllocHGlobal(Marshal.SizeOf<Header>());
        Marshal.Copy(pcm,0,data,pcm.Length);Marshal.StructureToPtr(new Header{Data=data,Length=(uint)pcm.Length},header,false);
        var buffer=new Buffer(header,data,pcm.Length,submitted,Peak(pcm));buffers.Add(buffer);
        Check(waveOutPrepareHeader(device,header,(uint)Marshal.SizeOf<Header>()));Check(waveOutWrite(device,header,(uint)Marshal.SizeOf<Header>()));submitted+=pcm.Length;
    }
    internal static double Peak(byte[] pcm){var peak=0;for(var i=0;i+1<pcm.Length;i+=2)peak=Math.Max(peak,Math.Abs((int)(short)(pcm[i]|pcm[i+1]<<8)));return Math.Clamp(peak/12000d,0,1);}
    void Receive(IntPtr handle,uint message,IntPtr instance,IntPtr header,IntPtr extra){if(message==0x3BD&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(Collect);}
    void Collect()
    {
        if(device==IntPtr.Zero)return;
        foreach(var buffer in buffers.ToArray())if((Marshal.PtrToStructure<Header>(buffer.Header).Flags&1)!=0)Release(buffer);
    }
    void Release(Buffer buffer){if(device!=IntPtr.Zero)waveOutUnprepareHeader(device,buffer.Header,(uint)Marshal.SizeOf<Header>());Marshal.FreeHGlobal(buffer.Header);Marshal.FreeHGlobal(buffer.Data);buffers.Remove(buffer);}
    internal void Clear(){if(device!=IntPtr.Zero)waveOutReset(device);foreach(var buffer in buffers.ToArray())Release(buffer);submitted=0;}
    public void Dispose(){Clear();if(device!=IntPtr.Zero){waveOutClose(device);device=IntPtr.Zero;}}
    static void Check(uint code){if(code!=0)throw new InvalidOperationException("Speaker unavailable. Check the default output device in Windows.");}
    delegate void WaveCallback(IntPtr handle,uint message,IntPtr instance,IntPtr header,IntPtr extra);
    [StructLayout(LayoutKind.Sequential,Pack=2)]struct Format{public ushort Tag,Channels;public uint Rate,Bytes;public ushort Align,Bits,Extra;}
    [StructLayout(LayoutKind.Sequential)]struct Header{public IntPtr Data;public uint Length,Recorded;public UIntPtr User;public uint Flags,Loops;public IntPtr Next;public UIntPtr Reserved;}
    [StructLayout(LayoutKind.Sequential)]struct Position{public uint Type,Value,Unused1;}
    [DllImport("winmm.dll")]static extern uint waveOutOpen(out IntPtr handle,uint id,ref Format format,WaveCallback callback,IntPtr instance,uint flags);
    [DllImport("winmm.dll")]static extern uint waveOutPrepareHeader(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")]static extern uint waveOutUnprepareHeader(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")]static extern uint waveOutWrite(IntPtr handle,IntPtr header,uint size);
    [DllImport("winmm.dll")]static extern uint waveOutGetPosition(IntPtr handle,ref Position position,uint size);
    [DllImport("winmm.dll")]static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")]static extern uint waveOutClose(IntPtr handle);
}

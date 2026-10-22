using System.Media;
using System.Runtime.InteropServices;
using System.Text;
namespace YoWidgets;

internal static class InterfaceAudio
{
    static SoundPlayer? player;static long last;
    internal static void Play(string sound,WidgetManager manager,bool preview=false)
    {
        if(sound is not("navigate" or "place" or "complete" or "theme")||manager.Snoozed||!preview&&!manager.Settings.InterfaceSounds||LiveVoiceSession.HasActiveSession||AudioRecorder.AnyRecording||VoiceInput.AnyListening||Fullscreen())return;
        var now=Environment.TickCount64;if(now-last<140)return;last=now;
        try
        {
            player?.Stop();player?.Dispose();var rate=22050;var seconds=sound=="theme"?.16:sound=="complete"?.12:.055;var count=(int)(rate*seconds);using var data=new MemoryStream();using(var writer=new BinaryWriter(data,Encoding.UTF8,true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++){var t=i/(double)rate;var frequency=sound=="theme"?440+700*t:sound=="navigate"?390:sound=="place"?260:t<.06?523:784;var envelope=Math.Min(1,t/.002)*Math.Exp(-(sound=="complete"?t% .06:t)/(sound=="theme"?.035:.016));writer.Write((short)(Math.Sin(2*Math.PI*frequency*t)*envelope*manager.Settings.InterfaceVolume*16000));}
            }
            player=new SoundPlayer(new MemoryStream(data.ToArray()));player.Play();
        }catch(Exception e)when(e is InvalidOperationException or IOException or System.ComponentModel.Win32Exception){}
    }
    static bool Fullscreen(){var h=GetForegroundWindow();if(h==IntPtr.Zero||!GetWindowRect(h,out var rect))return false;var screen=System.Windows.Forms.Screen.FromHandle(h).Bounds;return rect.Left<=screen.Left&&rect.Top<=screen.Top&&rect.Right>=screen.Right&&rect.Bottom>=screen.Bottom;}
    internal static void Stop(){player?.Stop();player?.Dispose();player=null;}
    [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
}

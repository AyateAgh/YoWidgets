using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
namespace YoWidgets;

internal sealed class InterfaceAccent:IDisposable
{
    internal static readonly Dictionary<string,string> Presets=Load();
    readonly Action changed;readonly Func<bool> automatic;int generation;bool disposed;
    internal string AutomaticColor {get;private set;}="#A7BED6";
    internal InterfaceAccent(Func<bool> automatic,Action changed){this.automatic=automatic;this.changed=changed;SystemEvents.UserPreferenceChanged+=PreferenceChanged;if(automatic())Refresh();}
    static Dictionary<string,string> Load(){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("YoWidgets.Accents.json")!;return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;}
    void PreferenceChanged(object sender,UserPreferenceChangedEventArgs args){if(automatic())Refresh();}
    internal string Resolve(string mode)=>mode=="auto"?AutomaticColor:Presets.GetValueOrDefault(mode,Presets["neutral"]);
    internal void Refresh(){var stamp=Interlocked.Increment(ref generation);_=Task.Run(()=>{var color=ReadWallpaper();Application.Current.Dispatcher.BeginInvoke(()=>{if(disposed||stamp!=generation)return;AutomaticColor=color;changed();});});}
    static string ReadWallpaper()
    {
        try{
            var path=new StringBuilder(32768);if(!SystemParametersInfo(0x73,(uint)path.Capacity,path,0))return WindowsColor();
            var file=path.ToString();if(string.IsNullOrEmpty(file)||file.StartsWith("\\\\")||!Path.IsPathFullyQualified(file)||!File.Exists(file)||new FileInfo(file).Length>32*1024*1024)return WindowsColor();
            using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.DecodePixelWidth=48;image.DecodePixelHeight=48;image.StreamSource=stream;image.EndInit();
            var bitmap=new System.Windows.Media.Imaging.FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
            var bins=new Dictionary<int,(int Count,long R,long G,long B)>();for(var i=0;i<pixels.Length;i+=4){int r=pixels[i+2],g=pixels[i+1],b=pixels[i];if(pixels[i+3]<128||Math.Max(r,Math.Max(g,b))<25||Math.Min(r,Math.Min(g,b))>235)continue;var key=(r>>5)<<6|(g>>5)<<3|(b>>5);var bin=bins.GetValueOrDefault(key);bins[key]=(bin.Count+1,bin.R+r,bin.G+g,bin.B+b);}
            if(bins.Count==0)return WindowsColor();var dominant=bins.Values.OrderByDescending(b=>b.Count).First();byte Tone(long channel)=>(byte)Math.Clamp(channel/dominant.Count*.65+89,90,230);return $"#{Tone(dominant.R):X2}{Tone(dominant.G):X2}{Tone(dominant.B):X2}";
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException){return WindowsColor();}
    }
    static string WindowsColor(){var color=SystemParameters.WindowGlassColor;return $"#{Math.Clamp((int)color.R,90,230):X2}{Math.Clamp((int)color.G,90,230):X2}{Math.Clamp((int)color.B,90,230):X2}";}
    public void Dispose(){disposed=true;Interlocked.Increment(ref generation);SystemEvents.UserPreferenceChanged-=PreferenceChanged;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SystemParametersInfo(uint action,uint parameter,StringBuilder value,uint flags);
}

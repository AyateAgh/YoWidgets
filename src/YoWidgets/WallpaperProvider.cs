using System.Runtime.InteropServices;
namespace YoWidgets;
internal sealed class WallpaperProvider : IDisposable {
 readonly IDesktopWallpaper? desktop;
 readonly Dictionary<string,BitmapSource> cache=[];
 public WallpaperProvider(){try{desktop=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;}catch(Exception e){App.Log("wallpaper",e);}}
 public Brush GetBrush(RectD sample,bool checker=false){
  if(checker)return Checker(sample);
  try{if(desktop==null)return new SolidColorBrush(Color.FromRgb(28,43,64));desktop.GetMonitorDevicePathCount(out var count);string? path=null;RectD screen=new(0,0,1920,1080);var best=-1d;
   for(uint i=0;i<count;i++){desktop.GetMonitorDevicePathAt(i,out var id);desktop.GetMonitorRECT(id,out var r);var area=Math.Max(0,Math.Min(sample.X+sample.Width,r.Right)-Math.Max(sample.X,r.Left))*Math.Max(0,Math.Min(sample.Y+sample.Height,r.Bottom)-Math.Max(sample.Y,r.Top));if(area<=best)continue;best=area;desktop.GetWallpaper(id,out path);screen=new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
   desktop.GetBackgroundColor(out var c);var background=Color.FromRgb((byte)c,(byte)(c>>8),(byte)(c>>16));if(string.IsNullOrEmpty(path)||!File.Exists(path))return new SolidColorBrush(background);
   var key=path+File.GetLastWriteTimeUtc(path).Ticks;if(!cache.TryGetValue(key,out var image)){var b=new BitmapImage();b.BeginInit();b.UriSource=new(path);b.CacheOption=BitmapCacheOption.OnLoad;b.DecodePixelWidth=3840;b.EndInit();b.Freeze();if(cache.Count>5)cache.Clear();cache[key]=image=b;}
   desktop.GetPosition(out var pos);var modes=new[]{"Center","Tile","Stretch","Fit","Fill","Span"};var mode=modes[Math.Clamp(pos,0,5)];if(mode=="Span"){var a=System.Windows.Forms.SystemInformation.VirtualScreen;screen=new(a.X,a.Y,a.Width,a.Height);}
   var layout=WallpaperLayout.Calculate(image.PixelWidth,image.PixelHeight,screen,mode);var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(new SolidColorBrush(background),null,new RectangleGeometry(new Rect(0,0,sample.Width,sample.Height))));
   if(mode=="Tile"){var tiles=new ImageBrush(image){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(-sample.X,-sample.Y,image.PixelWidth,image.PixelHeight),Stretch=Stretch.Fill};group.Children.Add(new GeometryDrawing(tiles,null,new RectangleGeometry(new Rect(0,0,sample.Width,sample.Height))));}
   else group.Children.Add(new ImageDrawing(image,new Rect(layout.X-sample.X,layout.Y-sample.Y,layout.Width,layout.Height)));
   var brush=new DrawingBrush(group){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,sample.Width,sample.Height),Stretch=Stretch.Fill};return brush;
  }catch(Exception e){App.Log("wallpaper",e);return new SolidColorBrush(Color.FromRgb(28,43,64));}
 }
 static Brush Checker(RectD sample){var d=new DrawingGroup();d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(38,71,94)),null,new RectangleGeometry(new Rect(0,0,48,48))));d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(205,229,222)),null,new RectangleGeometry(new Rect(0,0,24,24))));d.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(205,229,222)),null,new RectangleGeometry(new Rect(24,24,24,24))));return new DrawingBrush(d){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(-sample.X%48,-sample.Y%48,48,48)};}
 public void Dispose(){cache.Clear();if(desktop!=null)Marshal.ReleaseComObject(desktop);}
}
[ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IDesktopWallpaper{
 [PreserveSig]int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? id,[MarshalAs(UnmanagedType.LPWStr)]string path);
 [PreserveSig]int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? id,[MarshalAs(UnmanagedType.LPWStr)]out string path);
 [PreserveSig]int GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string id);
 [PreserveSig]int GetMonitorDevicePathCount(out uint count);
 [PreserveSig]int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string id,out Native.RECT rect);
 [PreserveSig]int SetBackgroundColor(uint color);
 [PreserveSig]int GetBackgroundColor(out uint color);
 [PreserveSig]int SetPosition(int position);
 [PreserveSig]int GetPosition(out int position);
}

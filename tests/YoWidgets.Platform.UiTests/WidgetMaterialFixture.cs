using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YoWidgets;
using YoWidgets.Core;
using Brushes=System.Windows.Media.Brushes;

internal static class WidgetMaterialFixture
{
    internal static void Show(App app,string root)
    {
        Ui.Theme();var manager=new WidgetManager(root,true,focusEnvironment:new FakeFocusEnvironment());
        var bitmap=new WriteableBitmap(640,360,96,96,PixelFormats.Bgra32,null);var pixels=new byte[640*360*4];
        for(var y=0;y<360;y++)for(var x=0;x<640;x++){var i=(y*640+x)*4;pixels[i]=(byte)(90+x/5);pixels[i+1]=(byte)(70+y/3);pixels[i+2]=(byte)(210-x/5);pixels[i+3]=255;}bitmap.WritePixels(new Int32Rect(0,0,640,360),pixels,640*4,0);
        var path=Path.Combine(root,"fixture.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(path))encoder.Save(output);
        var photoState=new WidgetState{Kind="photo",Pack="liquid",ImagePath=path,X=500,Y=400};var photo=new WidgetWindow(manager,photoState,true){Title="YoWidgets photo resize fixture",ShowInTaskbar=true};photo.Show();
        var focus=new WidgetWindow(manager,new(){Kind="focus",Pack="liquid",X=80,Y=150},true){Title="YoWidgets Focus material fixture",ShowInTaskbar=true};focus.Show();
        var center=new ControlCenterView(manager,true);var glass=new GlassSurface();var grid=new Grid{Margin=new Thickness(20)};grid.Children.Add(glass);center.Margin=new Thickness(18);grid.Children.Add(center);
        var controls=new Window{Content=grid,Title="YoWidgets Controls drag fixture",Width=398,Height=290,Left=500,Top=100,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=true};
        controls.Show();controls.UpdateLayout();glass.Update(358,250,"liquid");
        foreach(var slider in Descendants(center).OfType<WidgetSlider>()){slider.IsEnabled=true;slider.Value=40;}
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>File.WriteAllText(Path.Combine(root,"fixture-state.json"),System.Text.Json.JsonSerializer.Serialize(new{values=Descendants(center).OfType<WidgetSlider>().Select(s=>s.Value).ToArray(),photoWidth=photo.ActualWidth,photoHeight=photo.ActualHeight,savedWidth=photoState.PhotoWidth,savedHeight=photoState.PhotoHeight}));timer.Start();
        controls.Closed+=(_,_)=>{timer.Stop();photo.CloseOwned();focus.CloseOwned();manager.Dispose();app.Shutdown();};
    }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var item in Descendants(child))yield return item;}}
}

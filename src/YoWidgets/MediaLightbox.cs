using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class MediaLightbox : Window
{
    GeneratedMedia selected;
    readonly IReadOnlyList<GeneratedMedia> gallery;
    readonly Action<GeneratedMedia,AssistantAttachment?,string>? edit;
    readonly Action<string>? notice;
    readonly Grid stage=new(){ClipToBounds=true};
    readonly TextBlock title=Ui.Text("",13);
    readonly ScaleTransform zoom=new(1,1);
    readonly TranslateTransform pan=new();
    readonly TextBox prompt=Ui.Input("");
    readonly Button markup;
    MediaSurface? surface;
    AssistantAttachment? annotation;
    Point? drag;
    internal static void Show(GeneratedMedia media,IReadOnlyList<GeneratedMedia>? gallery=null,Action<GeneratedMedia,AssistantAttachment?,string>? edit=null,Action<string>? notice=null)
    {if(media.MimeType is "model/gltf-binary" or "application/ply"){AttachmentActions.Open(media.Attachment(),notice);return;}new MediaLightbox(media,(gallery??[media]).Where(m=>m.MimeType is not ("model/gltf-binary" or "application/ply")).ToArray(),edit,notice).Show();}
    internal MediaLightbox(GeneratedMedia media,IReadOnlyList<GeneratedMedia> gallery,Action<GeneratedMedia,AssistantAttachment?,string>? edit,Action<string>? notice)
    {
        selected=media;this.gallery=gallery;this.edit=edit;this.notice=notice;Title="Media viewer";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Background=Brushes.Black;Foreground=Ui.Brush("Text");Resources["WidgetInk"]=Ui.Brush("Text");
        Width=SystemParameters.PrimaryScreenWidth;Height=SystemParameters.PrimaryScreenHeight;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var layout=new Grid{Margin=new Thickness(20,12,20,16)};layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=layout;
        var top=new DockPanel{Margin=new Thickness(0,0,0,14)};var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(controls,Dock.Right);top.Children.Add(controls);top.Children.Add(title);title.VerticalAlignment=VerticalAlignment.Center;
        var fit=Ui.Button("Fit",()=>SetZoom(1));controls.Children.Add(fit);var slider=new Slider{Width=100,Minimum=.25,Maximum=4,Value=1,VerticalAlignment=VerticalAlignment.Center,ToolTip="Zoom"};slider.ValueChanged+=(_,e)=>SetZoom(e.NewValue);controls.Children.Add(slider);
        controls.Children.Add(WidgetVisuals.IconButton("copy","Copy media",()=>AttachmentActions.Copy(selected.Attachment(),notice),40));controls.Children.Add(WidgetVisuals.IconButton("download","Download original",()=>Download(selected,notice),40));controls.Children.Add(WidgetVisuals.IconButton("close","Close viewer",CloseViewer,40));layout.Children.Add(top);
        Grid.SetRow(stage,1);layout.Children.Add(stage);stage.PreviewMouseWheel+=(_,e)=>{SetZoom(Math.Clamp(zoom.ScaleX*(e.Delta>0?1.12:1/1.12),.25,4));slider.Value=zoom.ScaleX;e.Handled=true;};
        stage.MouseLeftButtonDown+=(_,e)=>{if(zoom.ScaleX<=1)return;drag=e.GetPosition(stage);stage.CaptureMouse();};stage.MouseMove+=(_,e)=>{if(drag is not Point point)return;var next=e.GetPosition(stage);pan.X+=next.X-point.X;pan.Y+=next.Y-point.Y;drag=next;};stage.MouseLeftButtonUp+=(_,_)=>{drag=null;stage.ReleaseMouseCapture();};
        var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,2);layout.Children.Add(footer);
        var thumbnails=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        foreach(var version in gallery) {
            var button=Ui.Button(version.Label+" "+(gallery.ToList().IndexOf(version)+1),()=>Select(version));button.ToolTip="View "+version.Label.ToLowerInvariant()+" version "+(gallery.ToList().IndexOf(version)+1);
            if(version.MimeType.StartsWith("image/"))try{button.Content=new System.Windows.Controls.Image{Source=MediaSurface.Bitmap(version.Path,100),Width=48,Height=38,Stretch=Stretch.Uniform};}catch(Exception e) when(e is not OutOfMemoryException){}thumbnails.Children.Add(button);
        }
        if(gallery.Count>1)footer.Children.Add(new ScrollViewer{Content=thumbnails,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=64});
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,8)};markup=Ui.Button("Markup",Markup);actions.Children.Add(markup);
        if(edit!=null) {
            var ratios=new ComboBox{ItemsSource=new[]{"Resize…","Square · 1:1","Portrait · 3:4","Story · 9:16","Landscape · 4:3","Widescreen · 16:9"},SelectedIndex=0,MinWidth=150,Margin=new Thickness(4)};
            ratios.SelectionChanged+=(_,_)=>{if(ratios.SelectedIndex<=0)return;prompt.Text="Resize this "+(selected.MimeType.StartsWith("video/")?"video":"image")+" to "+ratios.SelectedItem+", preserving its content and composition.";prompt.Focus();};actions.Children.Add(ratios);
        }
        footer.Children.Add(actions);
        if(edit!=null) {
            var composer=new DockPanel{MaxWidth=920};var send=Ui.Button("Edit in chat",()=>{var text=prompt.Text.Trim();if(text.Length==0){prompt.Focus();return;}edit(selected,annotation,text);CloseViewer();},true);DockPanel.SetDock(send,Dock.Right);composer.Children.Add(send);prompt.MinHeight=42;prompt.MaxLength=16000;prompt.ToolTip="Describe changes. The original stays intact.";System.Windows.Automation.AutomationProperties.SetName(prompt,"Describe media changes");composer.Children.Add(prompt);footer.Children.Add(composer);
        }
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;CloseViewer();}else if(e.Key is Key.Left or Key.Right&&Keyboard.FocusedElement is not TextBox){var index=gallery.ToList().FindIndex(m=>m.Id==selected.Id);Select(gallery[(index+(e.Key==Key.Right?1:gallery.Count-1))%gallery.Count]);e.Handled=true;}};
        SourceInitialized+=(_,_)=>{
            var handle=new WindowInteropHelper(this).Handle;var foreground=GetForegroundWindow();var monitor=System.Windows.Forms.Screen.FromHandle(foreground==handle?handle:foreground).Bounds;SetWindowPos(handle,IntPtr.Zero,monitor.Left,monitor.Top,monitor.Width,monitor.Height,0x0040);
        };
        Closed+=(_,_)=>surface?.Dispose();Select(media);
        if(SystemParameters.ClientAreaAnimation)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
    }
    void SetZoom(double value){zoom.ScaleX=zoom.ScaleY=value;if(value<=1){pan.X=pan.Y=0;}}
    void Select(GeneratedMedia media)
    {
        surface?.Dispose();stage.Children.Clear();selected=media;annotation=null;markup.IsEnabled=media.MimeType.StartsWith("image/")&&media.MimeType!="image/gif";title.Text=media.Label+" · "+media.Model;title.ToolTip="Generated media · "+media.Provider;SetZoom(1);surface=new(media.Path,media.MimeType);surface.RenderTransformOrigin=new Point(.5,.5);var transforms=new TransformGroup();transforms.Children.Add(zoom);transforms.Children.Add(pan);surface.RenderTransform=transforms;stage.Children.Add(surface);surface.ContextMenu=Menu(media,()=>SetZoom(1),notice);
    }
    void Markup()
    {
        if(!selected.MimeType.StartsWith("image/")||selected.MimeType=="image/gif")return;
        try {
            var drawing=new DrawingWindow(selected.Path){Owner=this};if(drawing.ShowDialog()!=true||drawing.Result==null)return;annotation=drawing.Result;
            var original=selected;var path=AttachmentActions.Cache(annotation);var marked=new GeneratedMedia("annotation:"+Guid.NewGuid().ToString("N"),path,"image/png","Local","Markup",original.Prompt,null,original.Id);
            surface?.Dispose();stage.Children.Clear();surface=new(path,"image/png");stage.Children.Add(surface);
            // Markup exports a real new PNG; Download/Copy now act on that copy, edits still reference the original.
            selected=marked;if(edit!=null){prompt.Text="Apply the changes marked on this image.";prompt.Focus();}title.Text="Markup · original preserved";
        }catch(Exception e) when(e is not OutOfMemoryException){MessageBox.Show(this,"Could not prepare markup. "+e.Message,"Media",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    async void CloseViewer(){if(SystemParameters.ClientAreaAnimation){BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(140)));await Task.Delay(140);}if(IsVisible)Close();}
    internal static void Download(GeneratedMedia media,Action<string>? notice=null)
    {try{var picker=new Microsoft.Win32.SaveFileDialog{FileName=(media.MimeType.StartsWith("audio/")?"Audio":media.MimeType is "model/gltf-binary" or "application/ply"?"Model":media.MimeType.StartsWith("video/")?"Video":"Image")+Path.GetExtension(media.Path),Filter="All files|*.*",OverwritePrompt=true};if(picker.ShowDialog()==true){File.Copy(media.Path,picker.FileName,true);notice?.Invoke("Saved");}}catch(IOException){notice?.Invoke("Could not save this file. Choose another location.");}}
    internal static ContextMenu Menu(GeneratedMedia media,Action open,Action<string>? notice)
    {var menu=new ContextMenu{Style=(Style)Application.Current.FindResource("AssistantFileMenu")};foreach(var (label,action) in new (string,Action)[]{("Open",open),("Copy",()=>AttachmentActions.Copy(media.Attachment(),notice)),("Download",()=>Download(media,notice))}){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}return menu;}
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}

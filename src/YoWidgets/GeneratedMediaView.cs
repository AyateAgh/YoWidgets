using System.Globalization;
namespace YoWidgets;

internal sealed class MediaSkeleton : ActiveVisual
{
    internal MediaSkeleton(){Height=230;Active=true;}
    protected override void OnRender(DrawingContext dc)
    {
        var bounds=new Rect(RenderSize);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(25,23,37)),null,bounds,18,18);
        dc.PushClip(new RectangleGeometry(bounds,18,18));var t=Time;
        for(var y=12d;y<ActualHeight;y+=12)for(var x=12d;x<ActualWidth;x+=12){var wave=Math.Sin(x*.025+y*.018-t*1.6);var radius=.65+1.1*(wave+1)/2;var alpha=(byte)(38+130*(wave+1)/2);dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha,171,145,240)),null,new Point(x,y),radius,radius);}dc.Pop();
    }
}
internal sealed class GeneratedMediaView(ChatMessage message,AiService ai,Action<GeneratedMedia,AssistantAttachment?,string> edit,Action<string> notice):StackPanel
{
    readonly Dictionary<GeneratedMediaJob,(Grid Preview,TextBlock Label,MediaSkeleton Skeleton)> jobs=[];
    internal void Refresh()
    {
        foreach(var job in message.Media) {
            if(!jobs.TryGetValue(job,out var ui)) {
                var preview=new Grid {ClipToBounds=true,MinHeight=180};var skeleton=new MediaSkeleton();preview.Children.Add(skeleton);
                var label=WidgetVisuals.Label(job.Stage,12);label.TextWrapping=TextWrapping.Wrap;label.Margin=new Thickness(12,10,12,10);
                var content=new StackPanel();content.Children.Add(preview);content.Children.Add(label);
                var card=new Border{Child=content,CornerRadius=new CornerRadius(18),ClipToBounds=true,BorderThickness=new Thickness(1),Margin=new Thickness(0,8,0,8)};card.SetResourceReference(BackgroundProperty,"AssistantInset");card.SetResourceReference(Border.BorderBrushProperty,"AssistantLine");Children.Add(card);jobs.Add(job,ui=(preview,label,skeleton));
            }
            ui.Label.Text=job.Error.Length>0?job.Error:job.Result!=null?job.Result.Label+" · "+job.Result.Model:job.Stage+(job.Percent.HasValue?" "+job.Percent.Value+"%":"");
            ui.Label.ToolTip=job.Result==null?"Media generation status":"Generated media · "+job.Result.Provider;ui.Skeleton.Active=!job.Finished;
            if(job.Result!=null&&ui.Preview.Tag==null) {
                var media=job.Result;ui.Preview.Tag=media;ui.Preview.Children.Clear();ui.Preview.Height=230;
                if(media.MimeType.StartsWith("image/")||media.MimeType.StartsWith("video/")){var surface=new MediaSurface(media.Path,media.MimeType,false);ui.Preview.Children.Add(surface);}else {ui.Preview.Height=115;var label=WidgetVisuals.Label(media.Label+"\n"+Path.GetExtension(media.Path).TrimStart('.').ToUpperInvariant()+" · "+new FileInfo(media.Path).Length.ToString("N0")+" bytes\nClick to open · right-click to download",13);label.TextAlignment=TextAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;ui.Preview.Children.Add(label);}
                var open=new Button{Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip="Open media",Content=new Grid(),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
                System.Windows.Automation.AutomationProperties.SetName(open,"Open generated "+media.Label.ToLowerInvariant());
                void Open()=>MediaLightbox.Show(media,ai.Messages.SelectMany(m=>m.Media).Where(j=>j.Result!=null).Select(j=>j.Result!).ToArray(),edit,notice);
                open.Click+=(_,_)=>Open();ui.Preview.Children.Add(open);open.ContextMenu=MediaLightbox.Menu(media,Open,notice);
            }else if(job.Finished&&job.Result==null){ui.Preview.Visibility=Visibility.Collapsed;}
        }
    }
}

// Images, real GIF playback and native video share the same viewer surface.
internal sealed class MediaSurface : Grid,IDisposable
{
    readonly string path,mime;
    readonly bool playback;
    readonly System.Windows.Controls.Image image=new(){Stretch=Stretch.Uniform};
    readonly MediaElement video=new(){LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close,Stretch=Stretch.Uniform,Volume=.7};
    readonly DispatcherTimer timer=new();
    System.Drawing.Image? gif;MemoryStream? gifStream;int frame;int[] delays=[];
    bool started,playing;
    CancellationTokenSource? loading;
    internal MediaSurface(string path,string mime,bool playback=true)
    {
        this.path=path;this.mime=mime;this.playback=playback;Children.Add(image);Loaded+=(_,_)=>Start();Unloaded+=(_,_)=>Dispose();
        timer.Tick+=(_,_)=>{try{NextGif();}catch(Exception e) when(e is not OutOfMemoryException){timer.Stop();}};video.MediaFailed+=(_,_)=>{Children.Clear();Children.Add(Ui.Text("Windows could not play this media. Download it to open in another player.",13,"Muted"));};
    }
    internal static BitmapSource Bitmap(string path,int decode=2048)
    {var source=new BitmapImage();source.BeginInit();source.CacheOption=BitmapCacheOption.OnLoad;source.DecodePixelWidth=decode;source.UriSource=new Uri(path);source.EndInit();source.Freeze();return source;}
    async void Start()
    {
        if(started)return;started=true;
        try {
            if(mime.StartsWith("video/")||mime.StartsWith("audio/")) {
                if(!playback){using var stop=new CancellationTokenSource();loading=stop;try{var clip=await Windows.Media.Editing.MediaClip.CreateFromFileAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(path).AsTask(stop.Token)).AsTask(stop.Token);var composition=new Windows.Media.Editing.MediaComposition();composition.Clips.Add(clip);try{using var frame=await composition.GetThumbnailAsync(TimeSpan.Zero,Math.Min(768,(int)clip.GetVideoEncodingProperties().Width),Math.Max(1,(int)(Math.Min(768d/clip.GetVideoEncodingProperties().Width,1)*clip.GetVideoEncodingProperties().Height)),Windows.Media.Editing.VideoFramePrecision.NearestFrame).AsTask(stop.Token);using var reader=new Windows.Storage.Streams.DataReader(frame.GetInputStreamAt(0));await reader.LoadAsync((uint)frame.Size).AsTask(stop.Token);var data=new byte[(int)frame.Size];reader.ReadBytes(data);using var memory=new MemoryStream(data);var thumbnail=new BitmapImage();thumbnail.BeginInit();thumbnail.CacheOption=BitmapCacheOption.OnLoad;thumbnail.StreamSource=memory;thumbnail.EndInit();thumbnail.Freeze();stop.Token.ThrowIfCancellationRequested();image.Source=thumbnail;}finally{composition.Clips.Clear();}}finally{if(ReferenceEquals(loading,stop))loading=null;}return;}
                Children.Clear();Children.Add(video);if(mime.StartsWith("audio/"))Children.Add(new TextBlock{Text="AI-generated audio",FontSize=24,Foreground=Ui.Brush("Text"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});video.Source=new Uri(path);video.Play();playing=true;
                if(playback){var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Background=Ui.Brush("Panel"),Margin=new Thickness(12)};
                    var toggle=WidgetVisuals.IconButton("pause","Play or pause media",()=>{if(playing)video.Pause();else video.Play();playing=!playing;});controls.Children.Add(toggle);
                    var seek=new Slider{Width=240,Minimum=0,Maximum=1,ToolTip="Seek media"};var updating=false;var clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
                    clock.Tick+=(_,_)=>{if(video.NaturalDuration.HasTimeSpan){updating=true;seek.Maximum=video.NaturalDuration.TimeSpan.TotalSeconds;seek.Value=video.Position.TotalSeconds;updating=false;}};seek.ValueChanged+=(_,_)=>{if(!updating)video.Position=TimeSpan.FromSeconds(seek.Value);};controls.Children.Add(seek);
                    var volume=new Slider{Width=70,Minimum=0,Maximum=1,Value=.7,ToolTip="Volume"};volume.ValueChanged+=(_,_)=>video.Volume=volume.Value;controls.Children.Add(volume);Children.Add(controls);clock.Start();Unloaded+=(_,_)=>clock.Stop();
                }else {video.IsMuted=true;video.MediaEnded+=(_,_)=>{video.Position=TimeSpan.Zero;video.Play();};}
            }else if(mime=="image/gif") {
                gifStream=new MemoryStream(File.ReadAllBytes(path));gif=System.Drawing.Image.FromStream(gifStream);if(gif.Width>4096||gif.Height>4096||(long)gif.Width*gif.Height>12000000)throw new InvalidOperationException("Image is too large to animate.");
                var count=gif.GetFrameCount(System.Drawing.Imaging.FrameDimension.Time);if(count>500||(long)gif.Width*gif.Height*count>120000000)throw new InvalidOperationException("Animation exceeds preview limits.");delays=new int[count];var raw=gif.GetPropertyItem(0x5100)?.Value;
                for(var i=0;i<count;i++)delays[i]=raw!=null&&raw.Length>=(i+1)*4?Math.Clamp(BitConverter.ToInt32(raw,i*4)*10,30,10000):100;
                NextGif();if(SystemParameters.ClientAreaAnimation)timer.Start();
            }else image.Source=Bitmap(path);
        }catch(OperationCanceledException){}catch(Exception e) when(e is not OutOfMemoryException){Dispose();Children.Clear();Children.Add(Ui.Text("Preview unavailable. Download the original file to view it.",13,"Muted"));}
    }
    void NextGif()
    {
        if(gif==null||delays.Length==0)return;gif.SelectActiveFrame(System.Drawing.Imaging.FrameDimension.Time,frame);
        var scale=Math.Min(1,1024d/Math.Max(gif.Width,gif.Height));var width=Math.Max(1,(int)(gif.Width*scale));var height=Math.Max(1,(int)(gif.Height*scale));using var bitmap=new System.Drawing.Bitmap(width,height);using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.DrawImage(gif,0,0,width,height);
        using var data=new MemoryStream();bitmap.Save(data,System.Drawing.Imaging.ImageFormat.Png);data.Position=0;var source=new BitmapImage();source.BeginInit();source.CacheOption=BitmapCacheOption.OnLoad;source.StreamSource=data;source.EndInit();source.Freeze();image.Source=source;timer.Interval=TimeSpan.FromMilliseconds(delays[frame]);frame=(frame+1)%delays.Length;
    }
    public void Dispose(){loading?.Cancel();loading=null;timer.Stop();video.Close();gif?.Dispose();gif=null;gifStream?.Dispose();gifStream=null;image.Source=null;started=false;}
}

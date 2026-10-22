using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using YoWidgets;

static class MediaChecks
{
    internal static AssistantAttachment Gif()
    {
        var encoder=new GifBitmapEncoder();
        foreach(var color in new[] { Colors.Red,Colors.Blue,Colors.Lime })
        {
            var pixels=new byte[96*64*4]; for(var i=0;i<pixels.Length;i+=4) { pixels[i]=color.B; pixels[i+1]=color.G; pixels[i+2]=color.R; pixels[i+3]=255; }
            var image=BitmapSource.Create(96,64,96,96,PixelFormats.Bgra32,null,pixels,96*4); encoder.Frames.Add(BitmapFrame.Create(image));
        }
        using var data=new MemoryStream(); encoder.Save(data);
        return new("color-sequence.gif","image/gif",Convert.ToBase64String(data.ToArray()),null,data.Length);
    }
    internal static async Task Run()
    {
        var file=AttachmentMedia.PrepareGif(Gif(),CancellationToken.None);
        if(file.Frames.Count!=3 || file.Frames.Any(f=>f.MimeType!="image/jpeg"))throw new Exception("GIF frames were not normalized");
        for(var i=0;i<3;i++)
        {
            using var memory=new MemoryStream(Convert.FromBase64String(file.Frames[i].Data)); var image=BitmapDecoder.Create(memory,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
            var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0); var pixel=new byte[4]; converted.CopyPixels(new Int32Rect(40,30,1,1),pixel,4,0);
            if(pixel[new[]{2,0,1}[i]]<200)throw new Exception("GIF storyboard lost its frame colors");
        }
        Console.WriteLine("PASS GIF: chronological JPEG frames preserve red / blue / green pixels");
        var folder=Path.GetFullPath("artifacts/assistant-media");Directory.CreateDirectory(folder);
        var storage=await StorageFolder.GetFolderFromPathAsync(folder); var video=await storage.CreateFileAsync("fixture.mp4",CreationCollisionOption.ReplaceExisting);
        var composition=new MediaComposition(); composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,255,0,0),TimeSpan.FromSeconds(2)));composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255,0,0,255),TimeSpan.FromSeconds(2)));
        var reason=await composition.RenderToFileAsync(video,MediaTrimmingPreference.Fast,MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga));
        if(reason!=Windows.Media.Transcoding.TranscodeFailureReason.None)throw new Exception("Video fixture encoding failed: "+reason);
        composition.Clips.Clear();
        var prepared=await AttachmentMedia.ReadAsync(video.Path,CancellationToken.None);
        if(prepared.Frames.Count!=6 || prepared.Frames.Any(f=>f.Data.Length==0))throw new Exception("Video storyboard is missing frames");
        Console.WriteLine("PASS video: native H.264 decode produced six chronological thumbnails");
    }
}

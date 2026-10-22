using System.Windows.Media.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
namespace YoWidgets;

// Prepare visual inputs locally. Original bytes remain the retry snapshot;
// previews and chronological storyboards share the same immutable frames.
internal static class AttachmentMedia
{
    internal static async Task<AssistantAttachment> ReadAsync(string path,CancellationToken token)
    {
        var file=await AssistantAttachment.ReadAsync(path,token);
        if(file.MimeType=="image/gif") return await Task.Run(()=>PrepareGif(file,token),token);
        if(file.MimeType.StartsWith("video/"))
        {
            var frames=new List<AssistantImage>();
            try
            {
                var clip=await MediaClip.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(token)).AsTask(token);
                var composition=new MediaComposition(); composition.Clips.Add(clip);
                try
                {
                    var duration=clip.OriginalDuration.TotalSeconds;
                    if(duration<=0) throw new InvalidOperationException("This video has no playable frames.");
                    var properties=clip.GetVideoEncodingProperties();
                    var scale=Math.Min(1,768d/Math.Max(1,Math.Max(properties.Width,properties.Height)));
                    var frameWidth=Math.Max(1,(int)(properties.Width*scale)); var frameHeight=Math.Max(1,(int)(properties.Height*scale));
                    for(var i=0;i<6;i++)
                    {
                        token.ThrowIfCancellationRequested(); var seconds=duration*i/6;
                        using var frame=await composition.GetThumbnailAsync(TimeSpan.FromSeconds(seconds),frameWidth,frameHeight,VideoFramePrecision.NearestFrame).AsTask(token);
                        using var reader=new DataReader(frame.GetInputStreamAt(0)); await reader.LoadAsync((uint)frame.Size).AsTask(token); var bytes=new byte[frame.Size]; reader.ReadBytes(bytes);
                        frames.Add(new(frame.ContentType,Convert.ToBase64String(bytes),seconds));
                    }
                    // Reject a file modified while its poster/storyboard was decoded.
                    if(!(await File.ReadAllBytesAsync(path,token)).AsSpan().SequenceEqual(Convert.FromBase64String(file.Data))) throw new InvalidOperationException("The video changed while being read. Attach it again.");
                }
                finally { composition.Clips.Clear(); }
            }
            catch(OperationCanceledException) { throw; }
            catch(InvalidOperationException) { throw; }
            catch { throw new InvalidOperationException("Windows could not decode this video. Try an MP4 with H.264 video."); }
            return Validate(file with { Frames=frames.ToArray() });
        }
        return file;
    }
    internal static AssistantAttachment PrepareGif(AssistantAttachment file,CancellationToken token)
    {
        using var memory=new MemoryStream(Convert.FromBase64String(file.Data));
        var decoder=new GifBitmapDecoder(memory,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
        int Meta(BitmapMetadata? metadata,string query,int fallback=0) { try { return metadata?.GetQuery(query) is object value ? Convert.ToInt32(value) : fallback; } catch { return fallback; } }
        var width=Meta(decoder.Metadata as BitmapMetadata,"/logscrdesc/Width",decoder.Frames[0].PixelWidth);
        var height=Meta(decoder.Metadata as BitmapMetadata,"/logscrdesc/Height",decoder.Frames[0].PixelHeight);
        if(width<=0 || height<=0 || width>4096 || height>4096 || (long)width*height>12_000_000 || decoder.Frames.Count>500) throw new InvalidOperationException("This GIF is too large to prepare. Use a smaller image or a short video.");
        if(decoder.Frames.Sum(f=>(long)f.PixelWidth*f.PixelHeight)>120_000_000) throw new InvalidOperationException("This GIF has too many full-size frames. Attach a smaller GIF or a short MP4.");
        var scale=Math.Min(1,768d/Math.Max(width,height)); var w=Math.Max(1,(int)(width*scale)); var h=Math.Max(1,(int)(height*scale));
        var sample=Enumerable.Range(0,Math.Min(8,decoder.Frames.Count)).Select(i=>(int)Math.Round(i*(decoder.Frames.Count-1d)/Math.Max(1,Math.Min(8,decoder.Frames.Count)-1))).ToHashSet();
        BitmapSource? canvas=null,restore=null; Rect previous=Rect.Empty; var disposal=0; double elapsed=0;
        var frames=new List<AssistantImage>();
        for(var i=0;i<decoder.Frames.Count;i++)
        {
            token.ThrowIfCancellationRequested(); var frame=decoder.Frames[i]; var metadata=frame.Metadata as BitmapMetadata;
            if(disposal==3) canvas=restore;
            if(disposal==2 && canvas!=null && !previous.IsEmpty)
            {
                var cleared=new DrawingVisual(); using(var dc=cleared.RenderOpen()) {
                    dc.DrawRectangle(Brushes.White,null,new Rect(0,0,w,h));
                    dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,w,h)),new RectangleGeometry(previous)));
                    dc.DrawImage(canvas,new Rect(0,0,w,h)); dc.Pop();
                }
                var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); bitmap.Render(cleared); bitmap.Freeze(); canvas=bitmap;
            }
            restore=canvas;
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White,null,new Rect(0,0,w,h));
                if(canvas!=null) dc.DrawImage(canvas,new Rect(0,0,w,h));
                previous=new Rect(Meta(metadata,"/imgdesc/Left")*scale,Meta(metadata,"/imgdesc/Top")*scale,frame.PixelWidth*scale,frame.PixelHeight*scale);
                dc.DrawImage(frame,previous);
            }
            var composed=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); composed.Render(visual); composed.Freeze(); canvas=composed;
            if(sample.Contains(i))
            {
                using var output=new MemoryStream(); var encoder=new JpegBitmapEncoder { QualityLevel=86 }; encoder.Frames.Add(BitmapFrame.Create(canvas)); encoder.Save(output);
                frames.Add(new("image/jpeg",Convert.ToBase64String(output.ToArray()),elapsed));
            }
            elapsed+=Math.Max(.02,Meta(metadata,"/grctlext/Delay",10)/100d); disposal=Meta(metadata,"/grctlext/Disposal");
        }
        return Validate(file with { Frames=frames.ToArray() });
    }
    static AssistantAttachment Validate(AssistantAttachment file)
    {
        if(file.Frames.Sum(f=>(long)f.Data.Length*3/4)>4*1024*1024) throw new InvalidOperationException("The media storyboard is too large. Attach a smaller image or shorter video.");
        return file;
    }
}

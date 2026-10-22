using System.Text;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
namespace YoWidgets;

// Deliberately bounded native GIF export, used only for an explicit animation request.
// A fixed 3/3/2 RGB palette and periodically cleared 9-bit LZW stream preserve all frames.
internal static class GeneratedAnimation
{
    internal static async Task<string> FromVideo(string path,CancellationToken token)
    {
        var clip=await MediaClip.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(path).AsTask(token)).AsTask(token);
        var duration=clip.OriginalDuration.TotalSeconds;if(duration<=0||duration>10.1)throw new InvalidOperationException("Animated GIF export supports clips up to 10 seconds. Request a shorter animation or a video.");
        var properties=clip.GetVideoEncodingProperties();var scale=Math.Min(1,480d/Math.Max(properties.Width,properties.Height));var width=Math.Max(1,(int)(properties.Width*scale));var height=Math.Max(1,(int)(properties.Height*scale));
        var composition=new MediaComposition();composition.Clips.Add(clip);using var output=new MemoryStream();using var writer=new BinaryWriter(output,Encoding.ASCII,true);
        writer.Write("GIF89a"u8);writer.Write((ushort)width);writer.Write((ushort)height);writer.Write((byte)0xF7);writer.Write((byte)0);writer.Write((byte)0);
        for(var i=0;i<256;i++){writer.Write((byte)(((i>>5)&7)*255/7));writer.Write((byte)(((i>>2)&7)*255/7));writer.Write((byte)((i&3)*255/3));}
        writer.Write(new byte[]{0x21,0xFF,11});writer.Write("NETSCAPE2.0"u8);writer.Write(new byte[]{3,1,0,0,0});
        try {
            var count=(int)Math.Ceiling(duration*10);
            for(var i=0;i<count;i++) {
                token.ThrowIfCancellationRequested();using var frame=await composition.GetThumbnailAsync(TimeSpan.FromSeconds(i/10d),width,height,VideoFramePrecision.NearestFrame).AsTask(token);
                using var reader=new DataReader(frame.GetInputStreamAt(0));await reader.LoadAsync((uint)frame.Size).AsTask(token);var raw=new byte[(int)frame.Size];reader.ReadBytes(raw);
                using var stream=new MemoryStream(raw);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);var source=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);source.Freeze();
                // Thumbnail dimensions can be adjusted by the decoder; scale to the fixed GIF canvas.
                BitmapSource pixels=source;if(source.PixelWidth!=width||source.PixelHeight!=height){pixels=new TransformedBitmap(source,new ScaleTransform((double)width/source.PixelWidth,(double)height/source.PixelHeight));pixels.Freeze();}
                var data=new byte[width*height*4];pixels.CopyPixels(data,width*4,0);WriteFrame(writer,width,height,data,10);
            }
            writer.Write((byte)0x3B);writer.Flush();var result=Path.ChangeExtension(path,".gif");var partial=result+".partial";try{await File.WriteAllBytesAsync(partial,output.ToArray(),token);token.ThrowIfCancellationRequested();File.Move(partial,result);}finally{if(File.Exists(partial))File.Delete(partial);}return result;
        }finally {composition.Clips.Clear();}
    }
    internal static void WriteFrame(BinaryWriter writer,int width,int height,byte[] bgra,int delay)
    {
        writer.Write(new byte[]{0x21,0xF9,4,4});writer.Write((ushort)delay);writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0x2C);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write((ushort)width);writer.Write((ushort)height);writer.Write((byte)0);writer.Write((byte)8);
        using var codes=new MemoryStream();int bits=0,buffer=0,literals=0;
        void Code(int value){buffer|=value<<bits;bits+=9;while(bits>=8){codes.WriteByte((byte)buffer);buffer>>=8;bits-=8;}}
        Code(256);for(var p=0;p<width*height;p++){if(literals==200){Code(256);literals=0;}var offset=p*4;Code((bgra[offset+2]>>5)<<5|(bgra[offset+1]>>5)<<2|(bgra[offset]>>6));literals++;}Code(257);if(bits>0)codes.WriteByte((byte)buffer);
        var encoded=codes.ToArray();for(var offset=0;offset<encoded.Length;offset+=255){var size=Math.Min(255,encoded.Length-offset);writer.Write((byte)size);writer.Write(encoded,offset,size);}writer.Write((byte)0);
    }
}

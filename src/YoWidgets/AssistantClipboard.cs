namespace YoWidgets;

internal static class AssistantClipboard
{
    internal static bool HasAttachment(IDataObject data)=>data.GetDataPresent(DataFormats.FileDrop)||data.GetDataPresent("PNG")||data.GetDataPresent(DataFormats.Bitmap);
    internal static (string[] Paths,AssistantAttachment? Image) Read(IDataObject data)
    {
        if(data.GetDataPresent(DataFormats.FileDrop)&&data.GetData(DataFormats.FileDrop) is string[] paths)return (paths.ToArray(),null);
        byte[]? bytes=null;
        if(data.GetDataPresent("PNG")) {
            if(data.GetData("PNG") is MemoryStream png) {if(png.Length>AssistantAttachment.MaxFileBytes)throw new InvalidOperationException("The clipboard image is larger than 8 MB.");bytes=png.ToArray();}
            else if(data.GetData("PNG") is byte[] array)bytes=array;
        }
        if(bytes==null&&data.GetData(DataFormats.Bitmap) is BitmapSource image) {
            if((long)image.PixelWidth*image.PixelHeight>16_000_000)throw new InvalidOperationException("Use a clipboard image smaller than 16 megapixels.");
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=new MemoryStream();encoder.Save(stream);bytes=stream.ToArray();
        }
        return ([],bytes==null?null:AssistantAttachment.FromBytes("Clipboard "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")+".png",bytes));
    }
}

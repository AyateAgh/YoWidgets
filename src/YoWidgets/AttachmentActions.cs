using System.Diagnostics;
using System.Text;
using System.Collections.Specialized;
using System.Security.Cryptography;
namespace YoWidgets;

internal static class AttachmentActions
{
    internal static ContextMenu Menu(AssistantAttachment file,Action<string>? notice=null)
    {
        var menu=new ContextMenu {Style=(Style)System.Windows.Application.Current.FindResource("AssistantFileMenu")};
        foreach(var (label,action) in new (string,Action)[]{("Open",()=>Open(file,notice)),("Copy",()=>Copy(file,notice)),("Download",()=>Download(file,notice))}) {
            var item=new MenuItem {Header=label};item.Click+=(_,_)=>Guard(action,notice);menu.Items.Add(item);
        }return menu;
    }
    static void Guard(Action action,Action<string>? notice)
    {try {action();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or FormatException) {var message="Could not open, copy or save this attachment. "+e.Message;if(notice!=null)notice(message);else MessageBox.Show(message,"Attachment",MessageBoxButton.OK,MessageBoxImage.Information);}}
    internal static string Cache(AssistantAttachment file)
    {
        var bytes=file.Bytes();var directory=Path.Combine(App.OwnedDirectory("assistant-cache"),Convert.ToHexString(SHA256.HashData(bytes))[..20]);Directory.CreateDirectory(directory);
        var name=Path.GetFileName(file.Name).TrimEnd(' ','.');var stem=Path.GetFileNameWithoutExtension(name).ToUpperInvariant();if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||stem is "CON" or "PRN" or "AUX" or "NUL"||System.Text.RegularExpressions.Regex.IsMatch(stem,"^(COM|LPT)[1-9]$"))name="attachment.bin";
        var path=Path.Combine(directory,name);if(!File.Exists(path))File.WriteAllBytes(path,bytes);return path;
    }
    internal static void Copy(AssistantAttachment file,Action<string>? notice=null)=>Guard(()=>{
        if(file.SourceUri!=null) {Clipboard.SetText(file.SourceUri);notice?.Invoke("Copied");return;}
        var data=new DataObject();var files=new StringCollection {Cache(file)};data.SetFileDropList(files);
        if(file.MimeType.StartsWith("image/"))try {using var stream=new MemoryStream(file.Bytes());var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();data.SetImage(image);if(file.MimeType=="image/png")data.SetData("PNG",new MemoryStream(file.Bytes()));}catch(NotSupportedException){}
        if(file.IsText)data.SetText(file.Text!);Clipboard.SetDataObject(data,true);notice?.Invoke("Copied");
    },notice);
    internal static void Download(AssistantAttachment file,Action<string>? notice=null)=>Guard(()=>{
        var picker=new Microsoft.Win32.SaveFileDialog {FileName=Path.GetFileName(file.Name),Filter="All files|*.*",Title="Download attachment",OverwritePrompt=true};
        if(picker.ShowDialog()==true) {File.WriteAllBytes(picker.FileName,file.Bytes());notice?.Invoke("Saved");}
    },notice);
    internal static void Open(AssistantAttachment file,Action<string>? notice=null)=>Guard(()=>{
        if(file.SourceUri!=null&&Uri.TryCreate(file.SourceUri,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https") {Process.Start(new ProcessStartInfo(uri.AbsoluteUri) {UseShellExecute=true});return;}
        var extension=Path.GetExtension(file.Name).ToLowerInvariant();
        if(file.MimeType.StartsWith("image/")||file.MimeType.StartsWith("video/")){MediaLightbox.Show(new("attachment",Cache(file),file.MimeType,"Local","Attachment",file.Name,null,null),notice:notice);return;}
        if(new[]{".docx",".xlsx",".pptx",".pdf",".png",".jpg",".jpeg",".gif",".webp",".bmp",".mp4",".webm",".mp3",".wav",".ogg",".flac",".glb",".ply"}.Contains(extension)) {Process.Start(new ProcessStartInfo(Cache(file)) {UseShellExecute=true});return;}
        var window=new Window {Title=file.Name,Width=Math.Min(1040,SystemParameters.WorkArea.Width*.85),Height=Math.Min(780,SystemParameters.WorkArea.Height*.85),Background=Ui.Brush("Background"),Foreground=Ui.Brush("Text"),WindowStartupLocation=WindowStartupLocation.CenterScreen};
        var layout=new DockPanel {Margin=new Thickness(16)};var toolbar=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,12)};
        toolbar.Children.Add(Ui.Button("Copy",()=>Copy(file,notice)));toolbar.Children.Add(Ui.Button("Download",()=>Download(file,notice)));DockPanel.SetDock(toolbar,Dock.Top);layout.Children.Add(toolbar);
        FrameworkElement content;
        if(file.MimeType.StartsWith("image/")) {
            using var stream=new MemoryStream(file.Bytes());var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();
            content=new System.Windows.Controls.Image {Source=image,Stretch=Stretch.Uniform,ContextMenu=Menu(file,notice)};
        }else {
            string text;try {text=file.Text??new UTF8Encoding(false,true).GetString(file.Bytes());}catch(DecoderFallbackException){text="Binary attachment · "+file.Size+" bytes";}
            content=new TextBox {Text=text,IsReadOnly=true,AcceptsReturn=true,FontFamily=new FontFamily("Cascadia Code, Consolas"),FontSize=13,Background=Brushes.Transparent,Foreground=Ui.Brush("Text"),BorderThickness=new Thickness(0),HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,ContextMenu=Menu(file,notice)};
        }
        layout.Children.Add(content);layout.ContextMenu=Menu(file,notice);window.Content=layout;window.Show();
    },notice);
    internal static AssistantAttachment Snapshot(string name,byte[] bytes)
    {
        try {return AssistantAttachment.FromBytes(Path.GetFileName(name),bytes);}catch(InvalidOperationException) {return new(Path.GetFileName(name),"application/octet-stream",Convert.ToBase64String(bytes),null,bytes.Length);}
    }
}

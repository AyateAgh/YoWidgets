using System.Windows.Media.Imaging;
namespace YoWidgets;

internal static class AttachmentPreview
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AssistantAttachment,BitmapSource> thumbnails=new();
    internal static FrameworkElement Tile(AssistantAttachment file,Action? remove=null,bool inMessage=false,Action<string>? notice=null)
    {
        var root=new Grid { Width=inMessage?124:100,Height=inMessage?100:82,Margin=new Thickness(0,0,7,4),Background=Brushes.Transparent,ToolTip=file.Name };
        root.ContextMenu=AttachmentActions.Menu(file,notice);root.Cursor=Cursors.Hand;root.Focusable=true;
        System.Windows.Automation.AutomationProperties.SetName(root,file.Name);System.Windows.Automation.AutomationProperties.SetHelpText(root,"Attachment. Enter opens it; Shift+F10 opens Open, Copy and Download.");
        root.PreviewKeyDown+=(_,e)=> {if(!ReferenceEquals(Keyboard.FocusedElement,root))return;if(e.Key==Key.Enter){e.Handled=true;AttachmentActions.Open(file,notice);}else if(e.Key==Key.Apps||e.Key==Key.F10&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)){e.Handled=true;root.ContextMenu.PlacementTarget=root;root.ContextMenu.IsOpen=true;}};
        root.MouseLeftButtonUp+=(_,e)=> {for(var parent=e.OriginalSource as DependencyObject;parent!=null&&parent!=root;parent=parent is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(parent):LogicalTreeHelper.GetParent(parent))if(parent is Button)return;e.Handled=true;AttachmentActions.Open(file,notice);};
        var face=new Grid(); face.RowDefinitions.Add(new()); face.RowDefinitions.Add(new() { Height=GridLength.Auto });
        var surface=new Border { Child=face,CornerRadius=new CornerRadius(12),ClipToBounds=true,BorderThickness=new Thickness(1) }; surface.SetResourceReference(Border.BackgroundProperty,"AssistantInset"); surface.SetResourceReference(Border.BorderBrushProperty,"AssistantLine"); root.Children.Add(surface);
        root.GotKeyboardFocus+=(_,_)=>surface.SetResourceReference(Border.BorderBrushProperty,"Accent");root.LostKeyboardFocus+=(_,_)=>{if(!root.IsKeyboardFocusWithin)surface.SetResourceReference(Border.BorderBrushProperty,"AssistantLine");};
        var thumbnail=Thumbnail(file);
        if(thumbnail!=null) face.Children.Add(new System.Windows.Controls.Image { Source=thumbnail,Stretch=Stretch.UniformToFill,Margin=new Thickness(1) });
        else
        {
            var extension=Path.GetExtension(file.Name).ToLowerInvariant();
            var brand=extension switch { ".ts" or ".tsx"=>"typescript", ".js" or ".jsx" or ".mjs"=>"javascript", ".py" or ".pyi"=>"python", ".rs"=>"rust", ".go"=>"go", ".html" or ".htm"=>"html5", ".css" or ".scss"=>"css3", ".md" or ".mdx"=>"markdown", ".cs"=>"csharp", ".java"=>"java", ".c" or ".h"=>"c", ".cpp" or ".hpp" or ".cc"=>"cplusplus", ".rb"=>"ruby", ".php"=>"php", ".swift"=>"swift", ".kt" or ".kts"=>"kotlin", ".vue"=>"vue", ".svelte"=>"svelte", ".json" or ".jsonc"=>"json", _=>null };
            var icon=brand==null ? WidgetVisuals.Icon(file.IsText?"file-code":"file",26) : new PremiumIcon(brand,26,"WidgetInk",true); icon.HorizontalAlignment=HorizontalAlignment.Center; icon.VerticalAlignment=VerticalAlignment.Center; face.Children.Add(icon);
        }
        var label=WidgetVisuals.Label(file.Name,10.5); label.Margin=new Thickness(7,4,7,5); Grid.SetRow(label,1); face.Children.Add(label);
        if(file.MimeType=="image/gif" || file.MimeType.StartsWith("video/"))
        {
            var badge=WidgetVisuals.Label(file.MimeType=="image/gif"?"GIF":"VIDEO",8.5,true); badge.Foreground=Brushes.White;
            face.Children.Add(new Border { Child=badge,Background=new SolidColorBrush(Color.FromArgb(180,12,16,24)),CornerRadius=new CornerRadius(4),Padding=new Thickness(4,2,4,2),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(5) });
        }
        if(remove!=null)
        {
            var close=WidgetVisuals.IconButton("close","Remove "+file.Name,remove,24); close.HorizontalAlignment=HorizontalAlignment.Right; close.VerticalAlignment=VerticalAlignment.Top; close.Margin=new Thickness(3); close.Background=new SolidColorBrush(Color.FromArgb(230,20,25,34));
            WidgetVisuals.UpdateIcon(close,"close",14,"RecordingInk"); root.Children.Add(close); HoverReveal.Attach(root,close);
        }
        return root;
    }
    static BitmapSource? Thumbnail(AssistantAttachment file)
    {
        if(thumbnails.TryGetValue(file,out var cached))return cached;
        var data=file.Frames.FirstOrDefault()?.Data ?? (file.MimeType.StartsWith("image/")?file.Data:null);
        if(data==null)return null;
        try { using var bytes=new MemoryStream(Convert.FromBase64String(data)); var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.DecodePixelWidth=248; image.StreamSource=bytes; image.EndInit(); image.Freeze(); thumbnails.Add(file,image); return image; }
        catch { return null; }
    }
}

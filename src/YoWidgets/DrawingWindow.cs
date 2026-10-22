using System.Windows.Ink;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
namespace YoWidgets;

internal sealed class DrawingWindow : Window
{
    sealed record ElementState(string Kind,string Text,double X,double Y,double Width,double Height,Color Color,double Size);
    sealed record Frame(StrokeCollection Strokes,ElementState[] Elements);
    readonly InkCanvas canvas=new() {Background=new SolidColorBrush(Color.FromRgb(32,32,34)),UseCustomCursor=false};
    readonly List<Frame> frames=[];
    readonly Dictionary<string,Button> tools=[];
    readonly List<Button> swatches=[];
    readonly Button undo,redo;
    Color ink=Colors.White;
    double thickness=3;
    string tool="pen",shapeKind="rectangle";
    int frame=-1;
    bool restoring,finishing;
    Point? origin;
    Shape? shape;
    public AssistantAttachment? Result {get;private set;}
    public DrawingWindow(string? backgroundPath=null)
    {
        Title="Draw";Width=Math.Min(1080,SystemParameters.WorkArea.Width*.9);Height=Math.Min(880,SystemParameters.WorkArea.Height*.9);MinWidth=620;MinHeight=450;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;Resources["WidgetInk"]=Ui.Brush("Text");
        var surface=new Border {Background=canvas.Background,CornerRadius=new CornerRadius(24),BorderBrush=new SolidColorBrush(Color.FromArgb(35,255,255,255)),BorderThickness=new Thickness(1),Padding=new Thickness(16)};
        var layout=new Grid();layout.RowDefinitions.Add(new() {Height=GridLength.Auto});layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new() {Height=GridLength.Auto});surface.Child=layout;Content=surface;
        var top=new Grid {Margin=new Thickness(0,0,0,10)};top.Children.Add(WidgetVisuals.IconButton("close","Close drawing",()=>_ = Finish(false)));((FrameworkElement)top.Children[0]).HorizontalAlignment=HorizontalAlignment.Left;
        var toolbar=new StackPanel {Orientation=Orientation.Horizontal};foreach(var (id,icon,label) in new[]{("select","mouse-pointer-2","Select"),("pen","pencil","Pen"),("text","type","Text"),("shape","shapes","Shapes"),("eraser","eraser","Eraser")}) {
            var button=WidgetVisuals.IconButton(icon,label,()=> {if(id=="shape"&&tool==id)OpenShapes();else SetTool(id);},36);tools[id]=button;toolbar.Children.Add(button);
        }
        top.Children.Add(new Border {Child=toolbar,CornerRadius=new CornerRadius(24),BorderBrush=Ui.Brush("Line"),BorderThickness=new Thickness(1),Padding=new Thickness(6,3,6,3),HorizontalAlignment=HorizontalAlignment.Center});
        var history=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};undo=WidgetVisuals.IconButton("undo-2","Undo",()=>Restore(frame-1));redo=WidgetVisuals.IconButton("redo-2","Redo",()=>Restore(frame+1));history.Children.Add(undo);history.Children.Add(redo);top.Children.Add(history);layout.Children.Add(top);
        var middle=new DockPanel();Grid.SetRow(middle,1);layout.Children.Add(middle);
        var size=new Slider {Minimum=1,Maximum=32,Value=3,Orientation=Orientation.Vertical,Width=30,Height=150,VerticalAlignment=VerticalAlignment.Center,Style=(Style)FindResource("DrawPenSize"),ToolTip="Brush size"};size.ValueChanged+=(_,e)=>{thickness=e.NewValue;ApplyInk();};middle.Children.Add(size);middle.Children.Add(canvas);
        var bottom=new Grid {Margin=new Thickness(0,12,0,0)};Grid.SetRow(bottom,2);layout.Children.Add(bottom);
        var palette=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        var custom=WidgetVisuals.IconButton("palette","Custom color",()=>{using var picker=new System.Windows.Forms.ColorDialog {FullOpen=true,Color=System.Drawing.Color.FromArgb(ink.R,ink.G,ink.B)};if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)ChooseColor(Color.FromRgb(picker.Color.R,picker.Color.G,picker.Color.B));},30);palette.Children.Add(custom);
        foreach(var color in new[]{Colors.White,Color.FromRgb(112,120,135),Color.FromRgb(148,73,16),Color.FromRgb(232,49,59),Color.FromRgb(255,115,35),Color.FromRgb(250,169,15),Color.FromRgb(31,179,100),Color.FromRgb(24,161,159),Color.FromRgb(13,180,218),Color.FromRgb(45,104,231),Color.FromRgb(84,65,237),Color.FromRgb(159,61,233),Color.FromRgb(220,42,133)}) {
            var button=WidgetVisuals.IconButton("",color.ToString(),()=>ChooseColor(color),29);button.Margin=new Thickness(2);button.Tag=color;button.Content=new Ellipse {Width=22,Height=22,Fill=new SolidColorBrush(color),Stroke=new SolidColorBrush(Color.FromArgb(80,255,255,255)),StrokeThickness=1};swatches.Add(button);palette.Children.Add(button);
        }bottom.Children.Add(palette);
        var done=WidgetVisuals.IconButton("check","Attach drawing",()=>_ = Finish(true),38,true);done.HorizontalAlignment=HorizontalAlignment.Right;bottom.Children.Add(done);
        canvas.StrokeCollected+=(_,_)=>Save();canvas.StrokeErased+=(_,_)=>Save();canvas.SelectionMoved+=(_,_)=>Save();canvas.SelectionResized+=(_,_)=>Save();
        canvas.PreviewMouseLeftButtonDown+=PointerDown;canvas.PreviewMouseMove+=PointerMove;canvas.PreviewMouseLeftButtonUp+=PointerUp;
        PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Escape){e.Handled=true;_ = Finish(false);}else if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key is Key.Z or Key.Y&&Keyboard.FocusedElement is not TextBox) {e.Handled=true;Restore(frame+(e.Key==Key.Y||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?1:-1));}};
        Loaded+=(_,_)=> {ApplyInk();ChooseColor(ink);Save();if(SystemParameters.ClientAreaAnimation)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));};
        SetTool("pen");
        if(backgroundPath!=null){var bitmap=MediaSurface.Bitmap(backgroundPath);canvas.Background=new ImageBrush(bitmap){Stretch=Stretch.Uniform};Title="Markup image";}
    }
    void SetTool(string id)
    {
        tool=id;canvas.EditingMode=id=="pen"?InkCanvasEditingMode.Ink:id=="eraser"?InkCanvasEditingMode.EraseByStroke:id=="select"?InkCanvasEditingMode.Select:InkCanvasEditingMode.None;
        foreach(var pair in tools)pair.Value.Background=pair.Key==id?new SolidColorBrush(Color.FromArgb(35,255,255,255)):Brushes.Transparent;canvas.Cursor=id=="text"?Cursors.IBeam:Cursors.Cross;
    }
    void OpenShapes()
    {
        var menu=new ContextMenu {Style=(Style)FindResource("AssistantFileMenu")};foreach(var id in new[]{"rectangle","ellipse"}) {var item=new MenuItem {Header=id=="rectangle"?"Rectangle":"Ellipse"};item.Click+=(_,_)=>shapeKind=id;menu.Items.Add(item);}menu.PlacementTarget=tools["shape"];menu.IsOpen=true;
    }
    void ChooseColor(Color color) {ink=color;foreach(var swatch in swatches)swatch.BorderBrush=(Color)swatch.Tag==color?new SolidColorBrush(Color.FromRgb(120,173,255)):Brushes.Transparent;ApplyInk();}
    void ApplyInk()=>canvas.DefaultDrawingAttributes=new DrawingAttributes {Color=ink,Width=thickness,Height=thickness,FitToCurve=true};
    void PointerDown(object sender,MouseButtonEventArgs e)
    {
        if(tool=="text") {
            for(var target=e.OriginalSource as DependencyObject;target!=null&&target!=canvas;target=target is Visual?VisualTreeHelper.GetParent(target):LogicalTreeHelper.GetParent(target))if(target is TextBox)return;
            var p=e.GetPosition(canvas);var text=new TextBox {Text="",Width=260,MinHeight=36,FontSize=24,Foreground=new SolidColorBrush(ink),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Ui.Brush("Line"),TextWrapping=TextWrapping.Wrap};InkCanvas.SetLeft(text,p.X);InkCanvas.SetTop(text,p.Y);canvas.Children.Add(text);text.LostKeyboardFocus+=(_,_)=> {text.BorderThickness=new Thickness(0);Save();};text.Focus();e.Handled=true;
        }else if(tool=="shape") {
            origin=e.GetPosition(canvas);shape=shapeKind=="ellipse"?new Ellipse():new Rectangle();shape.Stroke=new SolidColorBrush(ink);shape.StrokeThickness=thickness;shape.Fill=Brushes.Transparent;shape.Width=shape.Height=1;InkCanvas.SetLeft(shape,origin.Value.X);InkCanvas.SetTop(shape,origin.Value.Y);canvas.Children.Add(shape);canvas.CaptureMouse();e.Handled=true;
        }else if(tool=="eraser") {
            for(var p=e.OriginalSource as DependencyObject;p!=null&&p!=canvas;p=p is Visual?VisualTreeHelper.GetParent(p):LogicalTreeHelper.GetParent(p))if(p is FrameworkElement element&&canvas.Children.Contains(element)){canvas.Children.Remove(element);Save();e.Handled=true;break;}
        }
    }
    void PointerMove(object sender,MouseEventArgs e)
    {if(origin is not { } start||shape==null)return;var p=e.GetPosition(canvas);InkCanvas.SetLeft(shape,Math.Min(start.X,p.X));InkCanvas.SetTop(shape,Math.Min(start.Y,p.Y));shape.Width=Math.Max(1,Math.Abs(start.X-p.X));shape.Height=Math.Max(1,Math.Abs(start.Y-p.Y));}
    void PointerUp(object sender,MouseButtonEventArgs e)
    {if(origin==null)return;origin=null;shape=null;canvas.ReleaseMouseCapture();Save();e.Handled=true;}
    void Save()
    {
        if(restoring)return;var elements=canvas.Children.OfType<FrameworkElement>().Select(e=>new ElementState(e is TextBox?"text":e is Ellipse?"ellipse":"rectangle",(e as TextBox)?.Text??"",InkCanvas.GetLeft(e),InkCanvas.GetTop(e),e.Width,double.IsNaN(e.Height)?e.ActualHeight:e.Height,e is TextBox t?(t.Foreground as SolidColorBrush)?.Color??Colors.White:((Shape)e).Stroke is SolidColorBrush b?b.Color:Colors.White,e is TextBox label?label.FontSize:((Shape)e).StrokeThickness)).ToArray();
        if(frame<frames.Count-1)frames.RemoveRange(frame+1,frames.Count-frame-1);frames.Add(new(canvas.Strokes.Clone(),elements));if(frames.Count>80)frames.RemoveAt(0);frame=frames.Count-1;Buttons();
    }
    void Restore(int index)
    {
        if(index<0||index>=frames.Count)return;restoring=true;try {var saved=frames[index];canvas.Strokes=saved.Strokes.Clone();canvas.Children.Clear();foreach(var state in saved.Elements) {
            FrameworkElement element;if(state.Kind=="text") {var text=new TextBox {Text=state.Text,FontSize=state.Size,Foreground=new SolidColorBrush(state.Color),Background=Brushes.Transparent,BorderThickness=new Thickness(0),TextWrapping=TextWrapping.Wrap};text.LostKeyboardFocus+=(_,_)=>Save();element=text;}
            else {Shape item=state.Kind=="ellipse"?new Ellipse():new Rectangle();item.Stroke=new SolidColorBrush(state.Color);item.StrokeThickness=state.Size;item.Fill=Brushes.Transparent;element=item;}
            element.Width=state.Width;element.Height=state.Height;InkCanvas.SetLeft(element,state.X);InkCanvas.SetTop(element,state.Y);canvas.Children.Add(element);
        }frame=index;}finally {restoring=false;}Buttons();
    }
    void Buttons() {undo.IsEnabled=frame>0;redo.IsEnabled=frame>=0&&frame<frames.Count-1;}
    async Task Finish(bool attach)
    {
        if(finishing)return;finishing=true;
        if(attach) {
            canvas.EditingMode=InkCanvasEditingMode.None;Keyboard.ClearFocus();canvas.Select(new StrokeCollection(),[]);canvas.UpdateLayout();
            var bitmap=new RenderTargetBitmap(Math.Max(1,(int)canvas.ActualWidth),Math.Max(1,(int)canvas.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var bytes=new MemoryStream();encoder.Save(bytes);Result=AssistantAttachment.FromBytes("Drawing "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")+".png",bytes.ToArray());
        }
        if(SystemParameters.ClientAreaAnimation) {BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(140)));await Task.Delay(140);}DialogResult=attach;
    }
}

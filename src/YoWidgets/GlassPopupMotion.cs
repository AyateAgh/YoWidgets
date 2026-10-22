using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace YoWidgets;

// Owns popup motion, live material, outside dismissal and teardown for composer surfaces.
internal sealed class GlassPopupMotion
{
    readonly Popup popup;readonly Border card;readonly FrameworkElement anchor;readonly bool compact;
    readonly ScaleTransform scale=new(1,1);readonly TranslateTransform slide=new();
    double collapseX=.08,collapseY=.05,anchorOffset;
    LiveBackdrop? backdrop;HwndSource? source;Window? owner;int version;bool rendering;
    internal bool Closing {get;private set;}
    internal bool UsesLiveBackdrop=>backdrop!=null;
    internal bool MotionEnabled {get;set;}=true;
    internal event Action? CloseStarted;
    internal GlassPopupMotion(Popup popup,Border card,FrameworkElement anchor,bool compact=false)
    {
        this.popup=popup;this.card=card;this.anchor=anchor;this.compact=compact;popup.StaysOpen=true;popup.AllowsTransparency=true;
        var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(slide);card.RenderTransform=transforms;
        KeyboardNavigation.SetTabNavigation(card,KeyboardNavigationMode.Cycle);KeyboardNavigation.SetDirectionalNavigation(card,KeyboardNavigationMode.Cycle);
        popup.Opened+=(_,_)=>Open();popup.Closed+=(_,_)=>Cleanup();card.SizeChanged+=(_,_)=>Sync();
        card.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
    }
    bool Motion=>MotionEnabled&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
    async void Open()
    {
        var current=++version;Closing=false;card.UpdateLayout();owner=Window.GetWindow(anchor);if(owner!=null)owner.Deactivated+=Deactivated;
        InputManager.Current.PreProcessInput+=OutsideInput;anchor.Unloaded+=AnchorUnloaded;
        var point=anchor.PointToScreen(new Point(anchor.ActualWidth/2,anchor.ActualHeight/2));var start=card.PointFromScreen(point);
        card.RenderTransformOrigin=new Point(Math.Clamp(start.X/Math.Max(1,card.ActualWidth),0,1),Math.Clamp(start.Y/Math.Max(1,card.ActualHeight),0,1));
        collapseX=Math.Clamp(anchor.ActualWidth/Math.Max(1,card.ActualWidth),.03,.18);collapseY=Math.Clamp(anchor.ActualHeight/Math.Max(1,card.ActualHeight),.025,.18);anchorOffset=start.Y-card.RenderTransformOrigin.Y*card.ActualHeight;
        Animate(true);await card.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Loaded);if(current!=version||!popup.IsOpen)return;
        source=PresentationSource.FromVisual(card) as HwndSource;if(source!=null&&LiveBackdrop.Supported)try{backdrop=new LiveBackdrop("glass");backdrop.Readability(true);card.Background=new SolidColorBrush(Color.FromArgb(compact?(byte)202:(byte)175,20,26,35));Sync();}catch{backdrop?.Dispose();backdrop=null;}
        if(Motion){StartFrames();await Task.Delay(compact?150:260);if(current==version){StopFrames();Sync();}}
    }
    void Animate(bool open)
    {
        var reduced=compact?.96:collapseX;var reducedY=compact?.96:collapseY;var rise=compact?4:anchorOffset;var duration=TimeSpan.FromMilliseconds(compact?140:open?260:190);
        if(!Motion){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);slide.BeginAnimation(TranslateTransform.YProperty,null);card.BeginAnimation(UIElement.OpacityProperty,null);scale.ScaleX=scale.ScaleY=1;slide.Y=0;card.Opacity=1;return;}
        var easing=new CubicEase{EasingMode=open?EasingMode.EaseOut:EasingMode.EaseIn};
        DoubleAnimation Animation(double? from,double to)=>new(){From=from,To=to,Duration=duration,EasingFunction=easing};
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,Animation(open?reduced:null,open?1:reduced));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Animation(open?reducedY:null,open?1:reducedY));
        slide.BeginAnimation(TranslateTransform.YProperty,Animation(open?rise:null,open?0:rise));card.BeginAnimation(UIElement.OpacityProperty,Animation(open?0:null,open?1:0));
    }
    internal void Close()=>_ =CloseAsync();
    internal async Task CloseAsync()
    {
        if(!popup.IsOpen||Closing)return;var current=++version;Closing=true;CloseStarted?.Invoke();Animate(false);
        if(Motion){StartFrames();backdrop?.FadePopup(false);await Task.Delay(compact?140:190);}if(current==version){var focus=card.IsKeyboardFocusWithin;StopFrames();popup.IsOpen=false;if(focus&&anchor.IsLoaded&&anchor.IsVisible)anchor.Focus();}
    }
    void OutsideInput(object sender,PreProcessInputEventArgs e)
    {
        if(compact&&e.StagingItem.Input is KeyEventArgs {IsDown:true}){Close();return;}
        if(!popup.IsOpen||Closing||e.StagingItem.Input is not MouseButtonEventArgs {ButtonState:MouseButtonState.Pressed} mouse)return;
        DismissOutside(anchor.PointToScreen(mouse.GetPosition(anchor)));
    }
    internal void DismissOutside(Point screen){if(Inside(card,screen)||Inside(anchor,screen))return;Close();}
    static bool Inside(FrameworkElement target,Point screen){try{return new Rect(target.RenderSize).Contains(target.PointFromScreen(screen));}catch(InvalidOperationException){return false;}}
    void Deactivated(object? sender,EventArgs e)=>Close();
    void AnchorUnloaded(object sender,RoutedEventArgs e){if(popup.IsOpen)popup.IsOpen=false;}
    void Frame(object? sender,EventArgs e)=>Sync();
    void StartFrames(){if(rendering)return;rendering=true;CompositionTarget.Rendering+=Frame;}
    void StopFrames(){if(!rendering)return;rendering=false;CompositionTarget.Rendering-=Frame;}
    void Sync()
    {
        if(source==null||backdrop==null||!popup.IsOpen)return;
        if(source.RootVisual is Visual root)try{var rect=card.TransformToAncestor(root).TransformBounds(new Rect(card.RenderSize));backdrop.SyncPopup(source.Handle,compact?12:20,rect);}catch(InvalidOperationException){}
    }
    void Cleanup(){version++;Closing=false;StopFrames();anchor.Unloaded-=AnchorUnloaded;InputManager.Current.PreProcessInput-=OutsideInput;if(owner!=null)owner.Deactivated-=Deactivated;owner=null;source=null;backdrop?.Dispose();backdrop=null;}
}

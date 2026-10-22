using System.Windows.Media.Animation;
namespace YoWidgets;

// Keep the message/action envelope hittable, including the spacing between them.
internal static class HoverReveal
{
    internal static void Attach(FrameworkElement host,FrameworkElement tools)
    {
        if(host is Panel panel&&panel.Background==null)panel.Background=Brushes.Transparent;
        tools.Opacity=0;tools.IsHitTestVisible=false;
        var move=new TranslateTransform(0,3);tools.RenderTransform=move;
        var leave=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(260) };
        var version=0;bool shown=false;
        bool Inside()=>host.IsMouseOver||tools.IsMouseOver||tools.IsKeyboardFocusWithin;
        void SetVisible(bool show)
        {
            if(shown==show)return;shown=show;var animation=++version;
            if(show)tools.IsHitTestVisible=true;
            if(!SystemParameters.ClientAreaAnimation) {
                tools.BeginAnimation(UIElement.OpacityProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);
                tools.Opacity=show?1:0;move.Y=0;tools.IsHitTestVisible=show;return;
            }
            var fade=new DoubleAnimation(show?1:0,TimeSpan.FromMilliseconds(show?160:150)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
            // Remain interactive until the hide delay and fade have both ended.
            if(!show)fade.Completed+=(_,_)=> {if(animation==version&&!shown)tools.IsHitTestVisible=false;};
            tools.BeginAnimation(UIElement.OpacityProperty,fade);
            move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(show?0:3,TimeSpan.FromMilliseconds(180)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
        }
        void Update() {leave.Stop();if(Inside())SetVisible(true);else if(shown)leave.Start();}
        leave.Tick+=(_,_)=> {leave.Stop();if(!Inside())SetVisible(false);};
        host.MouseEnter+=(_,_)=>Update();host.MouseLeave+=(_,_)=>Update();host.IsKeyboardFocusWithinChanged+=(_,_)=>Update();
        tools.MouseEnter+=(_,_)=>Update();tools.MouseLeave+=(_,_)=>Update();tools.IsKeyboardFocusWithinChanged+=(_,_)=>Update();
        host.Loaded+=(_,_)=>Update();
        host.Unloaded+=(_,_)=> {leave.Stop();shown=false;version++;tools.BeginAnimation(UIElement.OpacityProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);tools.Opacity=0;tools.IsHitTestVisible=false;};
        host.IsVisibleChanged+=(_,_)=> {if(!host.IsVisible){leave.Stop();SetVisible(false);}};
    }
}

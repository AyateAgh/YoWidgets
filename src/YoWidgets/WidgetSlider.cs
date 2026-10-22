using System.Windows.Controls.Primitives;
namespace YoWidgets;

// Keep native Slider keyboard/automation semantics, and let the whole rail own a drag.
internal sealed class WidgetSlider:Slider
{
    bool dragging;
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if(!IsEnabled)return;
        Focus();dragging=CaptureMouse();
        if(dragging){Move(e);e.Handled=true;}
        base.OnPreviewMouseLeftButtonDown(e);
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        if(dragging&&IsMouseCaptured){Move(e);e.Handled=true;}
        base.OnPreviewMouseMove(e);
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(dragging){Move(e);dragging=false;ReleaseMouseCapture();e.Handled=true;}
        base.OnPreviewMouseLeftButtonUp(e);
    }
    protected override void OnLostMouseCapture(MouseEventArgs e){dragging=false;base.OnLostMouseCapture(e);}
    void Move(MouseEventArgs e)
    {
        if(Template.FindName("PART_Track",this) is not Track track)return;
        var value=track.ValueFromPoint(e.GetPosition(track));
        if(double.IsFinite(value))SetCurrentValue(ValueProperty,Math.Clamp(value,Minimum,Maximum));
    }
}

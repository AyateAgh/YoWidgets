namespace YoWidgets.Core;

public static class ShellLayoutRules
{
    public static RectD Fit(RectD desired,RectD work,double scale)
    {
        scale=double.IsFinite(scale)&&scale>0?scale:1;
        var minWidth=Math.Min(640*scale,work.Width);var minHeight=Math.Min(420*scale,work.Height);
        var width=Math.Clamp(double.IsFinite(desired.Width)?desired.Width:1180*scale,minWidth,work.Width);
        var height=Math.Clamp(double.IsFinite(desired.Height)?desired.Height:800*scale,minHeight,work.Height);
        var x=double.IsFinite(desired.X)?desired.X:work.X+(work.Width-width)/2;var y=double.IsFinite(desired.Y)?desired.Y:work.Y+(work.Height-height)/2;
        return new(Math.Clamp(x,work.X,work.X+work.Width-width),Math.Clamp(y,work.Y,work.Y+work.Height-height),width,height);
    }
}
